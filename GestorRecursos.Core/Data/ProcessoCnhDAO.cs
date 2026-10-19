using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: suspensão e cassação, que correm contra o condutor e não contra o carro.
public class ProcessoCnhDAO
{
    private const string Colunas = """
        P.ID, P.CLIENTE_ID, P.NUMERO_PROCESSO, P.TIPO, P.MOTIVO,
        P.DATA_NOTIFICACAO, P.PRAZO_DEFESA, P.PORTARIA, P.ORGAO_INSTAURADOR
        """;

    public async Task<List<ProcessoCnh>> ListarPorClienteAsync(
        int clienteId, CancellationToken cancelamento = default)
    {
        var sql = $"""
            SELECT {Colunas} FROM PROCESSOS_CNH P
             WHERE P.CLIENTE_ID = :clienteId
             ORDER BY P.PRAZO_DEFESA DESC NULLS LAST, P.ID DESC
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("clienteId", clienteId);

        var lista = new List<ProcessoCnh>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(Ler(reader));
        }

        return lista;
    }

    public async Task<ProcessoCnh?> BuscarPorIdAsync(int id, CancellationToken cancelamento = default)
    {
        var sql = $"SELECT {Colunas} FROM PROCESSOS_CNH P WHERE P.ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", id);

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        return await reader.ReadAsync(cancelamento) ? Ler(reader) : null;
    }

    /// <summary>Corrige o processo. As multas da pontuação não se mexem aqui.</summary>
    public async Task<bool> AtualizarAsync(ProcessoCnh processo, CancellationToken cancelamento = default)
    {
        const string sql = """
            UPDATE PROCESSOS_CNH SET
                NUMERO_PROCESSO = :numeroProcesso, TIPO = :tipo, MOTIVO = :motivo,
                DATA_NOTIFICACAO = :dataNotificacao, PRAZO_DEFESA = :prazoDefesa,
                PORTARIA = :portaria, ORGAO_INSTAURADOR = :orgaoInstaurador
             WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("numeroProcesso", processo.NumeroProcesso.Trim().ToUpperInvariant())
           .Param("tipo", processo.Tipo.Texto())
           .Param("motivo", processo.Motivo)
           .Param("dataNotificacao", processo.DataNotificacao)
           .Param("prazoDefesa", processo.PrazoDefesa)
           .Param("portaria", processo.Portaria)
           .Param("orgaoInstaurador", processo.OrgaoInstaurador)
           .Param("id", processo.Id);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    /// <summary>O número do processo é único: o DETRAN não repete.</summary>
    public async Task<bool> NumeroJaCadastradoAsync(
        string numero, int idIgnorado = 0, CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT COUNT(*) FROM PROCESSOS_CNH
             WHERE NUMERO_PROCESSO = :numero AND ID <> :idIgnorado
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("numero", numero.Trim().ToUpperInvariant()).Param("idIgnorado", idIgnorado);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancelamento)) > 0;
    }

    // ---------------------------------------------------------------------
    // Usados pelo ContratacaoDAO dentro da transação dele: o processo nasce
    // junto da contratação que o cobra, e os dois caem juntos ou não caem.
    // ---------------------------------------------------------------------

    internal static async Task<int> InserirAsync(
        OracleConnection conexao,
        OracleTransaction? transacao,
        ProcessoCnh processo,
        CancellationToken cancelamento)
    {
        const string sql = """
            INSERT INTO PROCESSOS_CNH
                (CLIENTE_ID, NUMERO_PROCESSO, TIPO, MOTIVO, DATA_NOTIFICACAO,
                 PRAZO_DEFESA, PORTARIA, ORGAO_INSTAURADOR)
            VALUES
                (:clienteId, :numeroProcesso, :tipo, :motivo, :dataNotificacao,
                 :prazoDefesa, :portaria, :orgaoInstaurador)
            RETURNING ID INTO :novoId
            """;

        await using var cmd = conexao.Comando(sql, transacao);
        cmd.Param("clienteId", processo.ClienteId)
           .Param("numeroProcesso", processo.NumeroProcesso.Trim().ToUpperInvariant())
           .Param("tipo", processo.Tipo.Texto())
           .Param("motivo", processo.Motivo)
           .Param("dataNotificacao", processo.DataNotificacao)
           .Param("prazoDefesa", processo.PrazoDefesa)
           .Param("portaria", processo.Portaria)
           .Param("orgaoInstaurador", processo.OrgaoInstaurador);

        var pId = new OracleParameter("novoId", OracleDbType.Int32)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        cmd.Parameters.Add(pId);

        await cmd.ExecuteNonQueryAsync(cancelamento);
        processo.Id = Convert.ToInt32(pId.Value.ToString());

        await LigarMultasAsync(conexao, transacao, processo, cancelamento);
        return processo.Id;
    }

    /// <summary>Guarda quais multas formaram a pontuação questionada.</summary>
    /// <remarks>
    /// São a matéria da defesa: derrubar uma delas pode derrubar o processo inteiro, e é
    /// por isso que elas ficam ligadas, e não só somadas num campo de pontos.
    /// </remarks>
    private static async Task LigarMultasAsync(
        OracleConnection conexao,
        OracleTransaction? transacao,
        ProcessoCnh processo,
        CancellationToken cancelamento)
    {
        const string sql = """
            INSERT INTO PROCESSO_MULTA (PROCESSO_CNH_ID, MULTA_ID) VALUES (:processoId, :multaId)
            """;

        foreach (var multa in processo.MultasRelacionadas)
        {
            await using var cmd = conexao.Comando(sql, transacao);
            cmd.Param("processoId", processo.Id).Param("multaId", multa.Id);
            await cmd.ExecuteNonQueryAsync(cancelamento);
        }
    }

    private static ProcessoCnh Ler(OracleDataReader reader) => new()
    {
        Id = reader.Inteiro(0),
        ClienteId = reader.Inteiro(1),
        NumeroProcesso = reader.Texto(2),
        Tipo = ValorBanco.De<TipoProcessoCnh>(reader.Texto(3)),
        Motivo = reader.TextoOuNulo(4),
        DataNotificacao = reader.DataOuNulo(5),
        PrazoDefesa = reader.DataOuNulo(6),
        Portaria = reader.TextoOuNulo(7),
        OrgaoInstaurador = reader.TextoOuNulo(8)
    };
}
