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

    public async Task<bool> AtualizarAsync(Multa multa, CancellationToken cancelamento = default)
    {
        const string sql = """
            UPDATE MULTAS SET
                VEICULO_ID = :veiculoId, AIT = :ait, ORGAO_AUTUADOR = :orgaoAutuador,
                CODIGO_ORGAO = :codigoOrgao, DATA_HORA_INFRACAO = :dataHoraInfracao,
                LOCAL_INFRACAO = :localInfracao, ENQUADRAMENTO = :enquadramento,
                DESCRICAO = :descricao, GRAVIDADE = :gravidade, PONTOS = :pontos,
                VALOR = :valor, PRAZO_DEFESA = :prazoDefesa, VENCIMENTO_BOLETO = :vencimentoBoleto
             WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        Preencher(cmd, multa);
        cmd.Param("id", multa.Id);

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
