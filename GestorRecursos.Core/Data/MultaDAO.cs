using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: autos de infração, sempre pendurados num veículo.
public class MultaDAO
{
    private const string Colunas = """
        M.ID, M.VEICULO_ID, M.AIT, M.ORGAO_AUTUADOR, M.CODIGO_ORGAO,
        M.DATA_HORA_INFRACAO, M.LOCAL_INFRACAO, M.ENQUADRAMENTO, M.DESCRICAO,
        M.GRAVIDADE, M.PONTOS, M.VALOR, M.PRAZO_DEFESA, M.VENCIMENTO_BOLETO
        """;

    public async Task<Multa?> BuscarPorIdAsync(int id, CancellationToken cancelamento = default)
    {
        var sql = $"SELECT {Colunas} FROM MULTAS M WHERE M.ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", id);

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        return await reader.ReadAsync(cancelamento) ? Ler(reader) : null;
    }

    public async Task<Multa?> BuscarPorAitAsync(string ait, CancellationToken cancelamento = default)
    {
        var sql = $"SELECT {Colunas} FROM MULTAS M WHERE M.AIT = :ait";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("ait", ait);

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        return await reader.ReadAsync(cancelamento) ? Ler(reader) : null;
    }

    /// <summary>Todas as multas de um veículo, da mais recente para a mais antiga.</summary>
    public async Task<List<Multa>> ListarPorVeiculoAsync(int veiculoId, CancellationToken cancelamento = default)
    {
        var sql = $"""
            SELECT {Colunas}
              FROM MULTAS M
             WHERE M.VEICULO_ID = :veiculoId
             ORDER BY M.DATA_HORA_INFRACAO DESC NULLS LAST, M.ID DESC
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("veiculoId", veiculoId);

        return await LerListaAsync(cmd, cancelamento);
    }

    /// <summary>Multas de todos os veículos ligados a um cliente, com a placa junto.</summary>
    public async Task<List<Multa>> ListarPorClienteAsync(int clienteId, CancellationToken cancelamento = default)
    {
        var sql = $"""
            SELECT {Colunas}, V.PLACA, V.MARCA_MODELO
              FROM MULTAS M
              JOIN VEICULOS V ON V.ID = M.VEICULO_ID
             WHERE M.VEICULO_ID IN (SELECT VEICULO_ID FROM CLIENTE_VEICULO WHERE CLIENTE_ID = :clienteId)
             ORDER BY M.DATA_HORA_INFRACAO DESC NULLS LAST, M.ID DESC
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("clienteId", clienteId);

        var lista = new List<Multa>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            var multa = Ler(reader);
            multa.Veiculo = new Veiculo
            {
                Id = multa.VeiculoId,
                Placa = reader.Texto(14),
                MarcaModelo = reader.TextoOuNulo(15)
            };
            lista.Add(multa);
        }

        return lista;
    }

    /// <summary>Multas do veículo para escolher quais entram no recurso.</summary>
    /// <remarks>
    /// Marca as que já são item de alguma contratação viva. Elas continuam na lista, e não
    /// escondidas: quem monta o recurso precisa saber que o auto existe e por que ele não
    /// pode entrar de novo.
    /// </remarks>
    public async Task<List<MultaParaRecurso>> ListarParaRecursoAsync(
        int veiculoId, CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT M.ID, M.AIT, M.ENQUADRAMENTO, M.DESCRICAO, M.GRAVIDADE, M.VALOR,
                   M.DATA_HORA_INFRACAO, M.PRAZO_DEFESA,
                   (SELECT COUNT(*) FROM ITENS_CONTRATACAO I
                     WHERE I.MULTA_ID = M.ID AND I.SITUACAO <> 'CANCELADO')
              FROM MULTAS M
             WHERE M.VEICULO_ID = :veiculoId
             ORDER BY M.DATA_HORA_INFRACAO DESC NULLS LAST, M.ID DESC
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("veiculoId", veiculoId);

        var lista = new List<MultaParaRecurso>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(new MultaParaRecurso
            {
                Id = reader.Inteiro(0),
                Ait = reader.Texto(1),
                Enquadramento = reader.TextoOuNulo(2),
                Descricao = reader.TextoOuNulo(3),
                Gravidade = ValorBanco.DeOuNulo<GravidadeMulta>(reader.TextoOuNulo(4)),
                Valor = reader.IsDBNull(5) ? null : reader.Decimal(5),
                DataHoraInfracao = reader.DataOuNulo(6),
                PrazoDefesa = reader.DataOuNulo(7),
                JaTemRecurso = reader.Inteiro(8) > 0
            });
        }

        return lista;
    }

    public async Task<int> CadastrarAsync(Multa multa, CancellationToken cancelamento = default)
    {
        const string sql = """
            INSERT INTO MULTAS (
                VEICULO_ID, AIT, ORGAO_AUTUADOR, CODIGO_ORGAO, DATA_HORA_INFRACAO,
                LOCAL_INFRACAO, ENQUADRAMENTO, DESCRICAO, GRAVIDADE, PONTOS,
                VALOR, PRAZO_DEFESA, VENCIMENTO_BOLETO)
            VALUES (
                :veiculoId, :ait, :orgaoAutuador, :codigoOrgao, :dataHoraInfracao,
                :localInfracao, :enquadramento, :descricao, :gravidade, :pontos,
                :valor, :prazoDefesa, :vencimentoBoleto)
            RETURNING ID INTO :novoId
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        Preencher(cmd, multa);

        var pId = new OracleParameter("novoId", OracleDbType.Int32)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        cmd.Parameters.Add(pId);

        await cmd.ExecuteNonQueryAsync(cancelamento);
        multa.Id = Convert.ToInt32(pId.Value.ToString());
        return multa.Id;
    }

    /// <summary>Atualiza a multa e leva junto o prazo da autuação que ainda está aberta.</summary>
    /// <remarks>
    /// O prazo da defesa prévia é o prazo da autuação: não há outra fonte para ele. Sem
    /// levar a correção adiante, arrumar a data no auto deixava o recurso com o prazo
    /// velho — ou sem prazo nenhum, que é pior, porque o alarme simplesmente não toca.
    ///
    /// Para no protocolo: depois de protocolado, o prazo é história e não se mexe mais.
    /// </remarks>
    public async Task<bool> AtualizarAsync(Multa multa, CancellationToken cancelamento = default)
    {
        const string sqlMulta = """
            UPDATE MULTAS SET
                VEICULO_ID = :veiculoId, AIT = :ait, ORGAO_AUTUADOR = :orgaoAutuador,
                CODIGO_ORGAO = :codigoOrgao, DATA_HORA_INFRACAO = :dataHoraInfracao,
                LOCAL_INFRACAO = :localInfracao, ENQUADRAMENTO = :enquadramento,
                DESCRICAO = :descricao, GRAVIDADE = :gravidade, PONTOS = :pontos,
                VALOR = :valor, PRAZO_DEFESA = :prazoDefesa, VENCIMENTO_BOLETO = :vencimentoBoleto
             WHERE ID = :id
            """;

        const string sqlPrazo = $"""
            UPDATE INSTANCIAS SET PRAZO_PROTOCOLO = :prazoDefesa
             WHERE FASE = 'AUTUACAO'
               AND DATA_PROTOCOLO IS NULL
               AND SITUACAO NOT IN {SituacoesSql.InstanciaEncerrada}
               AND ITEM_ID IN (SELECT ID FROM ITENS_CONTRATACAO
                                WHERE MULTA_ID = :id AND SITUACAO <> 'CANCELADO')
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var transacao = (OracleTransaction)await conexao.BeginTransactionAsync(cancelamento);

        try
        {
            int afetadas;
            await using (var cmd = conexao.Comando(sqlMulta, transacao))
            {
                Preencher(cmd, multa);
                cmd.Param("id", multa.Id);
                afetadas = await cmd.ExecuteNonQueryAsync(cancelamento);
            }

            await using (var cmd = conexao.Comando(sqlPrazo, transacao))
            {
                cmd.Param("prazoDefesa", multa.PrazoDefesa).Param("id", multa.Id);
                await cmd.ExecuteNonQueryAsync(cancelamento);
            }

            await transacao.CommitAsync(cancelamento);
            return afetadas > 0;
        }
        catch
        {
            await transacao.RollbackAsync(cancelamento);
            throw;
        }
    }

    /// <summary>Quem depende desta multa. Chamar antes de oferecer a exclusão.</summary>
    public async Task<ResumoExclusaoMulta> ResumirExclusaoAsync(
        int id, CancellationToken cancelamento = default)
    {
        // Conta o item cancelado também. Cancelar é desistir do serviço, não apagar o
        // que houve: a multa que já entrou numa contratação fica, e o que o banco
        // guarda continua explicando a cobrança.
        const string sql = """
            SELECT (SELECT COUNT(*) FROM ITENS_CONTRATACAO WHERE MULTA_ID = :id),
                   (SELECT COUNT(*) FROM PROCESSO_MULTA    WHERE MULTA_ID = :id)
              FROM DUAL
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", id);

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        if (!await reader.ReadAsync(cancelamento))
        {
            return new ResumoExclusaoMulta();
        }

        return new ResumoExclusaoMulta
        {
            Recursos = reader.Inteiro(0),
            ProcessosCnh = reader.Inteiro(1)
        };
    }

    /// <summary>Apaga a multa de vez. Só vale para a que ninguém usa.</summary>
    /// <remarks>
    /// A checagem vem antes, em <see cref="ResumirExclusaoAsync"/>, para a tela poder
    /// explicar o motivo. Aqui o DELETE ainda assim é protegido pelas chaves do banco.
    /// </remarks>
    public async Task<bool> ExcluirAsync(int id, CancellationToken cancelamento = default)
    {
        const string sql = "DELETE FROM MULTAS WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", id);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    public async Task<bool> AitJaCadastradoAsync(string ait, int idIgnorado = 0, CancellationToken cancelamento = default)
    {
        const string sql = "SELECT COUNT(*) FROM MULTAS WHERE AIT = :ait AND ID <> :idIgnorado";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("ait", ait).Param("idIgnorado", idIgnorado);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancelamento)) > 0;
    }

    // ---------------------------------------------------------------------

    private static void Preencher(OracleCommand cmd, Multa multa)
    {
        cmd.Param("veiculoId", multa.VeiculoId)
           .Param("ait", multa.Ait)
           .Param("orgaoAutuador", multa.OrgaoAutuador)
           .Param("codigoOrgao", multa.CodigoOrgao)
           .Param("dataHoraInfracao", multa.DataHoraInfracao)
           .Param("localInfracao", multa.LocalInfracao)
           .Param("enquadramento", multa.Enquadramento)
           .Param("descricao", multa.Descricao)
           .Param("gravidade", multa.Gravidade?.Texto())
           .Param("pontos", multa.Pontos)
           .Param("valor", multa.Valor)
           .Param("prazoDefesa", multa.PrazoDefesa)
           .Param("vencimentoBoleto", multa.VencimentoBoleto);
    }

    private static async Task<List<Multa>> LerListaAsync(OracleCommand cmd, CancellationToken cancelamento)
    {
        var lista = new List<Multa>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(Ler(reader));
        }

        return lista;
    }

    private static Multa Ler(OracleDataReader reader) => new()
    {
        Id = reader.Inteiro(0),
        VeiculoId = reader.Inteiro(1),
        Ait = reader.Texto(2),
        OrgaoAutuador = reader.TextoOuNulo(3),
        CodigoOrgao = reader.TextoOuNulo(4),
        DataHoraInfracao = reader.DataOuNulo(5),
        LocalInfracao = reader.TextoOuNulo(6),
        Enquadramento = reader.TextoOuNulo(7),
        Descricao = reader.TextoOuNulo(8),
        Gravidade = ValorBanco.DeOuNulo<GravidadeMulta>(reader.TextoOuNulo(9)),
        Pontos = reader.InteiroOuNulo(10),
        Valor = reader.IsDBNull(11) ? null : reader.Decimal(11),
        PrazoDefesa = reader.DataOuNulo(12),
        VencimentoBoleto = reader.DataOuNulo(13)
    };
}
