using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: o roteiro dos serviços diretos — renovação, reciclagem, Uber.
public class EtapaDAO
{
    /// <summary>Marca ou desmarca uma etapa do roteiro.</summary>
    /// <remarks>
    /// A data de conclusão acompanha a marca, e some junto quando ela desmarca: etapa
    /// aberta com data de quando foi concluída seria mentira no histórico. Marcar grava
    /// hoje; se não foi hoje que aconteceu, a data é corrigida pela edição da etapa.
    /// </remarks>
    public async Task<bool> MarcarAsync(
        int etapaId, bool concluida, CancellationToken cancelamento = default)
    {
        const string sql = """
            UPDATE ETAPAS SET
                CONCLUIDA = :concluida,
                DATA_CONCLUSAO = CASE WHEN :concluida = 'S' THEN TRUNC(SYSDATE) ELSE NULL END
             WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("concluida", concluida ? "S" : "N").Param("id", etapaId);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    /// <summary>Corrige as duas datas da etapa: até quando é, e quando foi feita.</summary>
    /// <remarks>
    /// A marca acompanha a data de conclusão, e não o contrário: pôr data é dizer que
    /// está feito, tirar é dizer que não está. Deixar os dois se contradizerem daria
    /// etapa aberta com data de conclusão, que é o estado que ninguém sabe ler.
    /// </remarks>
    public async Task<bool> AtualizarDatasAsync(
        int etapaId,
        DateTime? prazo,
        DateTime? dataConclusao,
        CancellationToken cancelamento = default)
    {
        const string sql = """
            UPDATE ETAPAS SET
                PRAZO = :prazo,
                DATA_CONCLUSAO = :dataConclusao,
                CONCLUIDA = CASE WHEN :dataConclusao IS NULL THEN 'N' ELSE 'S' END
             WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("prazo", prazo?.Date)
           .Param("dataConclusao", dataConclusao?.Date)
           .Param("id", etapaId);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    // ---------------------------------------------------------------------
    // Usado pelo ContratacaoDAO dentro da transação dele.
    // ---------------------------------------------------------------------

    /// <summary>Copia o roteiro do serviço para o item recém-contratado.</summary>
    /// <remarks>
    /// É cópia, e não referência: mudar o roteiro de um serviço amanhã não pode reescrever
    /// o que já estava sendo acompanhado hoje. O item guarda o caminho que foi combinado.
    ///
    /// Um INSERT ... SELECT resolve em uma ida ao banco, sem laço na aplicação.
    /// </remarks>
    internal static async Task CriarDoModeloAsync(
        OracleConnection conexao,
        OracleTransaction? transacao,
        int itemId,
        int tipoServicoId,
        CancellationToken cancelamento)
    {
        const string sql = """
            INSERT INTO ETAPAS (ITEM_ID, ETAPA_MODELO_ID, CONCLUIDA)
            SELECT :itemId, E.ID, 'N'
              FROM ETAPAS_MODELO E
             WHERE E.TIPO_SERVICO_ID = :tipoServicoId
             ORDER BY E.ORDEM
            """;

        await using var cmd = conexao.Comando(sql, transacao);
        cmd.Param("itemId", itemId).Param("tipoServicoId", tipoServicoId);

        await cmd.ExecuteNonQueryAsync(cancelamento);
    }
}
