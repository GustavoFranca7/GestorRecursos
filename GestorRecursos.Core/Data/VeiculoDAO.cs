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

    // ---------------------------------------------------------------------
    // A seção Veículos
    // ---------------------------------------------------------------------

    // Os agregados vêm em subconsultas escalares, e não em junções com GROUP BY: são
    // cinco contagens de tabelas diferentes, e agrupar por todas as colunas do veículo
    // para somá-las sairia mais longo e mais fácil de errar. A tabela é pequena.
    private static readonly string SqlLista = $"""
        SELECT V.ID, V.PLACA, V.RENAVAM, V.MARCA_MODELO, V.COR, V.ANO, V.OBSERVACAO,
               (SELECT MIN(CV.CLIENTE_ID)
                  FROM CLIENTE_VEICULO CV
                 WHERE CV.VEICULO_ID = V.ID AND CV.VINCULO = 'PROPRIETARIO'),
               (SELECT MIN(C.NOME)
                  FROM CLIENTE_VEICULO CV
                  JOIN CLIENTES C ON C.ID = CV.CLIENTE_ID
                 WHERE CV.VEICULO_ID = V.ID AND CV.VINCULO = 'PROPRIETARIO'),
               (SELECT COUNT(*)
                  FROM CLIENTE_VEICULO CV
                 WHERE CV.VEICULO_ID = V.ID AND CV.VINCULO = 'CONDUTOR'),
               (SELECT COUNT(*) FROM MULTAS M WHERE M.VEICULO_ID = V.ID),
               (SELECT COUNT(*)
                  FROM MULTAS M
                  JOIN ITENS_CONTRATACAO IT ON IT.MULTA_ID = M.ID
                  JOIN INSTANCIAS I         ON I.ITEM_ID = IT.ID
                 WHERE M.VEICULO_ID = V.ID
                   AND IT.SITUACAO <> 'CANCELADO'
                   AND I.SITUACAO NOT IN {SituacoesSql.InstanciaEncerrada})
          FROM VEICULOS V
         ORDER BY V.PLACA
        """;

    /// <summary>Todos os veículos, com quem está ligado a eles e o que já acumularam.</summary>
    public async Task<List<VeiculoDaLista>> ListarAsync(CancellationToken cancelamento = default)
    {
        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(SqlLista);
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);

        var lista = new List<VeiculoDaLista>();
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(new VeiculoDaLista
            {
                Id = reader.Inteiro(0),
                Placa = reader.Texto(1),
                Renavam = reader.TextoOuNulo(2),
                MarcaModelo = reader.TextoOuNulo(3),
                Cor = reader.TextoOuNulo(4),
                Ano = reader.TextoOuNulo(5),
                Observacao = reader.TextoOuNulo(6),
                ProprietarioId = reader.InteiroOuNulo(7),
                ProprietarioNome = reader.TextoOuNulo(8),
                Condutores = reader.Inteiro(9),
                Multas = reader.Inteiro(10),
                RecursosAbertos = reader.Inteiro(11)
            });
        }

        return lista;
    }

    /// <summary>O histórico do carro: os vínculos e todas as multas, de todos os clientes.</summary>
    /// <remarks>
    /// Sob demanda, e não junto da lista: multa é o que mais há no banco, e carregar as
    /// de todos os veículos para mostrar as de um seria pagar pelo que ninguém pediu.
    /// </remarks>
    public async Task<HistoricoDoVeiculo> AbrirAsync(int veiculoId, CancellationToken cancelamento = default)
    {
        const string sqlDonos = """
            SELECT CV.ID, CV.CLIENTE_ID, C.NOME, C.CPF, C.WHATSAPP, CV.VINCULO
              FROM CLIENTE_VEICULO CV
              JOIN CLIENTES C ON C.ID = CV.CLIENTE_ID
             WHERE CV.VEICULO_ID = :id
             ORDER BY CASE CV.VINCULO WHEN 'PROPRIETARIO' THEN 0 ELSE 1 END, C.NOME
            """;

        // A situação que vale é a da instância mais recente: o recurso anda de AUTUACAO
        // para JARI e para CETRAN, e é na última que ele está agora.
        const string sqlMultas = """
            SELECT M.ID, M.AIT, M.DESCRICAO, M.ENQUADRAMENTO, M.DATA_HORA_INFRACAO,
                   M.GRAVIDADE, M.PONTOS, M.VALOR, M.PRAZO_DEFESA,
                   MAX(CO.CLIENTE_ID)     KEEP (DENSE_RANK LAST ORDER BY NVL(I.ID, 0)),
                   MAX(CL.NOME)           KEEP (DENSE_RANK LAST ORDER BY NVL(I.ID, 0)),
                   MAX(I.FASE)            KEEP (DENSE_RANK LAST ORDER BY I.ID),
                   MAX(I.SITUACAO)        KEEP (DENSE_RANK LAST ORDER BY I.ID),
                   MAX(I.PRAZO_PROTOCOLO) KEEP (DENSE_RANK LAST ORDER BY I.ID)
              FROM MULTAS M
              LEFT JOIN ITENS_CONTRATACAO IT ON IT.MULTA_ID = M.ID
              LEFT JOIN CONTRATACOES CO      ON CO.ID = IT.CONTRATACAO_ID
              LEFT JOIN CLIENTES CL          ON CL.ID = CO.CLIENTE_ID
              LEFT JOIN INSTANCIAS I         ON I.ITEM_ID = IT.ID
             WHERE M.VEICULO_ID = :id
             GROUP BY M.ID, M.AIT, M.DESCRICAO, M.ENQUADRAMENTO, M.DATA_HORA_INFRACAO,
                      M.GRAVIDADE, M.PONTOS, M.VALOR, M.PRAZO_DEFESA
             ORDER BY M.DATA_HORA_INFRACAO DESC NULLS LAST, M.ID DESC
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);

        var donos = new List<DonoDoVeiculo>();
        await using (var cmd = conexao.Comando(sqlDonos))
        {
            cmd.Param("id", veiculoId);

            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                donos.Add(new DonoDoVeiculo
                {
                    VinculoId = reader.Inteiro(0),
                    ClienteId = reader.Inteiro(1),
                    Nome = reader.Texto(2),
                    Cpf = reader.Texto(3),
                    Whatsapp = reader.TextoOuNulo(4),
                    Vinculo = reader.Texto(5)
                });
            }
        }

        var multas = new List<MultaDoVeiculo>();
        await using (var cmd = conexao.Comando(sqlMultas))
        {
            cmd.Param("id", veiculoId);

            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                multas.Add(new MultaDoVeiculo
                {
                    Id = reader.Inteiro(0),
                    Ait = reader.Texto(1),
                    Descricao = reader.TextoOuNulo(2),
                    Enquadramento = reader.TextoOuNulo(3),
                    DataHoraInfracao = reader.DataOuNulo(4),
                    Gravidade = reader.TextoOuNulo(5),
                    Pontos = reader.InteiroOuNulo(6),
                    Valor = reader.DecimalOuNulo(7),
                    PrazoDefesa = reader.DataOuNulo(8),
                    ClienteId = reader.InteiroOuNulo(9),
                    ClienteNome = reader.TextoOuNulo(10),
                    Fase = reader.TextoOuNulo(11),
                    Situacao = reader.TextoOuNulo(12),
                    PrazoProtocolo = reader.DataOuNulo(13)
                });
            }
        }

        return new HistoricoDoVeiculo { Donos = donos, Multas = multas };
    }

    /// <summary>Cadastra o veículo sem ligar a ninguém.</summary>
    /// <remarks>
    /// O caminho de dentro da multa é o <see cref="CadastrarComVinculoAsync"/>, que já
    /// chega com o cliente. Aqui o carro pode nascer solto: na seção Veículos ele é
    /// cadastrado antes de se saber de quem é — o caso do carro que aparece numa
    /// notificação antes de o dono procurar o escritório.
    /// </remarks>
    public async Task<int> CadastrarAsync(Veiculo veiculo, CancellationToken cancelamento = default)
    {
        const string sql = """
            INSERT INTO VEICULOS (PLACA, RENAVAM, MARCA_MODELO, COR, ANO, OBSERVACAO)
            VALUES (:placa, :renavam, :marcaModelo, :cor, :ano, :observacao)
            RETURNING ID INTO :novoId
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("placa", veiculo.Placa)
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
        veiculo.Id = Convert.ToInt32(pId.Value.ToString());
        return veiculo.Id;
    }

    /// <summary>A placa já está no sistema? O UNIQUE garante, isto só avisa antes.</summary>
    public async Task<bool> PlacaJaCadastradaAsync(
        string placa, int idIgnorado = 0, CancellationToken cancelamento = default)
    {
        const string sql = "SELECT COUNT(*) FROM VEICULOS WHERE PLACA = :placa AND ID <> :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("placa", placa).Param("id", idIgnorado);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancelamento)) > 0;
    }
}
