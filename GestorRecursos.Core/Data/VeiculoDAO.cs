using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: veículos e o vínculo deles com os clientes.
public class VeiculoDAO
{
    private const string Colunas = "ID, PLACA, RENAVAM, MARCA_MODELO, COR, ANO, OBSERVACAO";

    public async Task<Veiculo?> BuscarPorPlacaAsync(string placa, CancellationToken cancelamento = default)
    {
        var sql = $"SELECT {Colunas} FROM VEICULOS WHERE PLACA = :placa";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("placa", placa.ToUpperInvariant());

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        return await reader.ReadAsync(cancelamento) ? Ler(reader) : null;
    }

    public async Task<Veiculo?> BuscarPorIdAsync(int id, CancellationToken cancelamento = default)
    {
        var sql = $"SELECT {Colunas} FROM VEICULOS WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", id);

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        return await reader.ReadAsync(cancelamento) ? Ler(reader) : null;
    }

    /// <summary>Cadastra o veículo e já o vincula ao cliente, numa transação só.</summary>
    /// <remarks>
    /// É o caminho do "cadastro rápido" dentro da tela de multa (RF-16b): a placa
    /// chega junto com a multa, e obrigar a sair da tela para cadastrar o carro
    /// antes seria o tipo de atrito que faz alguém voltar para o papel.
    /// </remarks>
    public async Task<int> CadastrarComVinculoAsync(
        Veiculo veiculo,
        int clienteId,
        VinculoVeiculo vinculo,
        CancellationToken cancelamento = default)
    {
        const string sqlVeiculo = """
            INSERT INTO VEICULOS (PLACA, RENAVAM, MARCA_MODELO, COR, ANO, OBSERVACAO)
            VALUES (:placa, :renavam, :marcaModelo, :cor, :ano, :observacao)
            RETURNING ID INTO :novoId
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var transacao = (OracleTransaction)await conexao.BeginTransactionAsync(cancelamento);

        try
        {
            int idVeiculo;
            await using (var cmd = conexao.Comando(sqlVeiculo, transacao))
            {
                cmd.Param("placa", veiculo.Placa.ToUpperInvariant())
                   .Param("renavam", veiculo.Renavam)
                   .Param("marcaModelo", veiculo.MarcaModelo)
                   .Param("cor", veiculo.Cor)
                   .Param("ano", veiculo.Ano)
                   .Param("observacao", veiculo.Observacao);

                var pId = new OracleParameter("novoId", OracleDbType.Int32)
                {
                    Direction = System.Data.ParameterDirection.Output
                };
                cmd.Parameters.Add(pId);

                await cmd.ExecuteNonQueryAsync(cancelamento);
                idVeiculo = Convert.ToInt32(pId.Value.ToString());
            }

            await VincularAsync(conexao, transacao, clienteId, idVeiculo, vinculo, cancelamento);

            await transacao.CommitAsync(cancelamento);
            veiculo.Id = idVeiculo;
            return idVeiculo;
        }
        catch
        {
            await transacao.RollbackAsync(cancelamento);
            throw;
        }
    }

    public async Task<bool> AtualizarAsync(Veiculo veiculo, CancellationToken cancelamento = default)
    {
        const string sql = """
            UPDATE VEICULOS SET
                PLACA = :placa, RENAVAM = :renavam, MARCA_MODELO = :marcaModelo,
                COR = :cor, ANO = :ano, OBSERVACAO = :observacao
             WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("placa", veiculo.Placa.ToUpperInvariant())
           .Param("renavam", veiculo.Renavam)
           .Param("marcaModelo", veiculo.MarcaModelo)
           .Param("cor", veiculo.Cor)
           .Param("ano", veiculo.Ano)
           .Param("observacao", veiculo.Observacao)
           .Param("id", veiculo.Id);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    /// <summary>Liga um veículo já existente a um cliente.</summary>
    /// <remarks>
    /// Recusa um segundo proprietário: o carro é de uma pessoa só. Condutor indicado
    /// pode ser vários, e é esse o caso que o vínculo duplo existe para cobrir.
    ///
    /// A checagem fica aqui, e não só na tela, porque é regra do domínio — a tela avisa
    /// antes para ser gentil, mas quem garante é a camada que grava.
    /// </remarks>
    public async Task VincularAsync(
        int clienteId,
        int veiculoId,
        VinculoVeiculo vinculo,
        CancellationToken cancelamento = default)
    {
        if (vinculo == VinculoVeiculo.Proprietario
            && await ProprietarioDeAsync(veiculoId, clienteId, cancelamento) is { } dono)
        {
            throw new InvalidOperationException(
                $"Este veículo já é de {dono}. Um carro tem um proprietário só — "
                + "ligue o cliente como condutor indicado.");
        }

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await VincularAsync(conexao, null, clienteId, veiculoId, vinculo, cancelamento);
    }

    /// <summary>Quem é o dono do carro, se não for o próprio cliente. Nulo quando não há.</summary>
    public async Task<string?> ProprietarioDeAsync(
        int veiculoId, int exceto, CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT MIN(C.NOME)
              FROM CLIENTE_VEICULO CV
              JOIN CLIENTES C ON C.ID = CV.CLIENTE_ID
             WHERE CV.VEICULO_ID = :veiculoId
               AND CV.VINCULO = 'PROPRIETARIO'
               AND CV.CLIENTE_ID <> :exceto
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("veiculoId", veiculoId).Param("exceto", exceto);

        var achado = await cmd.ExecuteScalarAsync(cancelamento);
        return achado is null or DBNull ? null : achado.ToString();
    }

    /// <summary>Veículos do sistema para escolher qual ligar a este cliente.</summary>
    /// <remarks>
    /// Busca por placa ou RENAVAM, que é como o carro é procurado na prática. Sem termo,
    /// traz os primeiros em ordem de placa — a lista existe para evitar que o mesmo carro
    /// seja cadastrado duas vezes.
    /// </remarks>
    public async Task<List<VeiculoParaVincular>> ListarParaVincularAsync(
        int clienteId, string? termo = null, CancellationToken cancelamento = default)
    {
        // O nome do dono atual vem junto: é o que permite recusar o vínculo de
        // proprietário explicando de quem o carro já é.
        const string sql = """
            SELECT V.ID, V.PLACA, V.RENAVAM, V.MARCA_MODELO, V.COR, V.ANO,
                   (SELECT COUNT(*) FROM CLIENTE_VEICULO CV
                     WHERE CV.VEICULO_ID = V.ID AND CV.CLIENTE_ID = :clienteId),
                   (SELECT COUNT(DISTINCT CV.CLIENTE_ID) FROM CLIENTE_VEICULO CV
                     WHERE CV.VEICULO_ID = V.ID),
                   (SELECT COUNT(*) FROM MULTAS M WHERE M.VEICULO_ID = V.ID),
                   (SELECT MIN(C.NOME) FROM CLIENTE_VEICULO CV
                      JOIN CLIENTES C ON C.ID = CV.CLIENTE_ID
                     WHERE CV.VEICULO_ID = V.ID
                       AND CV.VINCULO = 'PROPRIETARIO'
                       AND CV.CLIENTE_ID <> :clienteId)
              FROM VEICULOS V
             WHERE :termo IS NULL
                OR UPPER(V.PLACA) LIKE :procura
                OR V.RENAVAM LIKE :procura
             ORDER BY V.PLACA
             FETCH FIRST 60 ROWS ONLY
            """;

        var limpo = string.IsNullOrWhiteSpace(termo) ? null : termo.Trim().ToUpperInvariant();

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("clienteId", clienteId)
           .Param("termo", limpo)
           .Param("procura", limpo is null ? null : $"%{limpo}%");

        var lista = new List<VeiculoParaVincular>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(new VeiculoParaVincular
            {
                Id = reader.Inteiro(0),
                Placa = reader.Texto(1),
                Renavam = reader.TextoOuNulo(2),
                MarcaModelo = reader.TextoOuNulo(3),
                Cor = reader.TextoOuNulo(4),
                Ano = reader.TextoOuNulo(5),
                JaLigadoAoCliente = reader.Inteiro(6) > 0,
                Clientes = reader.Inteiro(7),
                Multas = reader.Inteiro(8),
                ProprietarioAtual = reader.TextoOuNulo(9)
            });
        }

        return lista;
    }

    /// <summary>Tira o veículo da ficha do cliente. O veículo e as multas dele ficam.</summary>
    /// <remarks>
    /// É o desfazer do engano mais comum: ligar o carro ao cliente errado. Não apaga
    /// nada — o mesmo carro pode estar ligado a outros clientes, e as multas pertencem
    /// a ele, não à ficha.
    /// </remarks>
    public async Task<bool> DesvincularDoClienteAsync(
        int clienteId, int veiculoId, CancellationToken cancelamento = default)
    {
        const string sql = """
            DELETE FROM CLIENTE_VEICULO
             WHERE CLIENTE_ID = :clienteId AND VEICULO_ID = :veiculoId
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("clienteId", clienteId).Param("veiculoId", veiculoId);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    public async Task<bool> DesvincularAsync(int vinculoId, CancellationToken cancelamento = default)
    {
        const string sql = "DELETE FROM CLIENTE_VEICULO WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", vinculoId);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    /// <summary>Quem são os donos e condutores desta placa.</summary>
    /// <remarks>Responde à pergunta que mais atrapalha o dia dela: "de quem é este carro?"</remarks>
    public async Task<List<(int ClienteId, string ClienteNome, VinculoVeiculo Vinculo)>> ListarClientesDaPlacaAsync(
        string placa,
        CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT C.ID, C.NOME, CV.VINCULO
              FROM CLIENTE_VEICULO CV
              JOIN CLIENTES C ON C.ID = CV.CLIENTE_ID
              JOIN VEICULOS V ON V.ID = CV.VEICULO_ID
             WHERE V.PLACA = :placa
             ORDER BY C.NOME
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("placa", placa.ToUpperInvariant());

        var lista = new List<(int, string, VinculoVeiculo)>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add((reader.Inteiro(0), reader.Texto(1), ValorBanco.De<VinculoVeiculo>(reader.Texto(2))));
        }

        return lista;
    }

    // ---------------------------------------------------------------------

    private static async Task VincularAsync(
        OracleConnection conexao,
        OracleTransaction? transacao,
        int clienteId,
        int veiculoId,
        VinculoVeiculo vinculo,
        CancellationToken cancelamento)
    {
        // MERGE porque o mesmo par cliente-veículo-vínculo não pode repetir, e o
        // banco tem UNIQUE nesses três. Sem isto, salvar duas vezes daria erro.
        const string sql = """
            MERGE INTO CLIENTE_VEICULO d
            USING (SELECT :clienteId AS CLIENTE_ID, :veiculoId AS VEICULO_ID, :vinculo AS VINCULO FROM DUAL) s
               ON (d.CLIENTE_ID = s.CLIENTE_ID AND d.VEICULO_ID = s.VEICULO_ID AND d.VINCULO = s.VINCULO)
            WHEN NOT MATCHED THEN INSERT (CLIENTE_ID, VEICULO_ID, VINCULO)
                VALUES (:clienteId, :veiculoId, :vinculo)
            """;

        await using var cmd = conexao.Comando(sql, transacao);
        cmd.Param("clienteId", clienteId)
           .Param("veiculoId", veiculoId)
           .Param("vinculo", vinculo.Texto());

        await cmd.ExecuteNonQueryAsync(cancelamento);
    }

    private static Veiculo Ler(OracleDataReader reader) => new()
    {
        Id = reader.Inteiro(0),
        Placa = reader.Texto(1),
        Renavam = reader.TextoOuNulo(2),
        MarcaModelo = reader.TextoOuNulo(3),
        Cor = reader.TextoOuNulo(4),
        Ano = reader.TextoOuNulo(5),
        Observacao = reader.TextoOuNulo(6)
    };
}
