using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: o registro das execucoes do backup semanal.
public class BackupDAO
{
    /// <summary>Dias sem backup a partir dos quais a tela avisa.</summary>
    /// <remarks>
    /// Dez, e não sete: a tarefa roda no domingo às 20:00 e, com o computador desligado,
    /// só roda quando ele liga de novo. Avisar no oitavo dia acusaria atraso em toda
    /// viagem de fim de semana, e aviso que aparece sem motivo deixa de ser lido.
    ///
    /// Dez dias significam um ciclo semanal inteiro perdido mais três dias de folga.
    /// </remarks>
    public const int DiasParaAvisar = 10;

    private const string SqlRegistrar = """
        INSERT INTO BACKUPS (QUANDO, MAQUINA, TABELAS, LINHAS, BYTES, DOCUMENTOS_NOVOS, FALHAS)
        VALUES (:quando, :maquina, :tabelas, :linhas, :bytes, :documentos, :falhas)
        """;

    // FETCH FIRST, e nao MAX(QUANDO): com o indice descendente a leitura e uma linha,
    // e o resto do resumo vem junto sem segunda consulta.
    private const string SqlUltimo = """
        SELECT ID, QUANDO, MAQUINA, TABELAS, LINHAS, BYTES, DOCUMENTOS_NOVOS, FALHAS
          FROM BACKUPS
         ORDER BY QUANDO DESC
         FETCH FIRST 1 ROWS ONLY
        """;

    /// <summary>Grava que o backup rodou. Chamado pelo próprio programa de backup.</summary>
    public async Task RegistrarAsync(ExecucaoDoBackup execucao, CancellationToken cancelamento = default)
    {
        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(SqlRegistrar);

        // A data vai como parametro, e nao SYSTIMESTAMP: o banco pode estar em UTC, e
        // "rodou domingo as 20:00" precisa ser o horario de quem olha o aviso.
        cmd.Param("quando", execucao.Quando)
           .Param("maquina", execucao.Maquina)
           .Param("tabelas", execucao.Tabelas)
           .Param("linhas", execucao.Linhas)
           .Param("bytes", execucao.Bytes)
           .Param("documentos", execucao.DocumentosNovos)
           .Param("falhas", execucao.Falhas);

        await cmd.ExecuteNonQueryAsync(cancelamento);
    }

    /// <summary>A execução mais recente, ou nulo quando nenhuma foi registrada ainda.</summary>
    public async Task<ExecucaoDoBackup?> UltimoAsync(CancellationToken cancelamento = default)
    {
        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(SqlUltimo);
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);

        if (!await reader.ReadAsync(cancelamento))
        {
            return null;
        }

        return new ExecucaoDoBackup
        {
            Id = reader.Inteiro(0),
            Quando = reader.Data(1),
            Maquina = reader.Texto(2),
            Tabelas = reader.Inteiro(3),
            Linhas = reader.Inteiro(4),
            Bytes = Convert.ToInt64(reader.GetValue(5)),
            DocumentosNovos = reader.Inteiro(6),
            Falhas = reader.Inteiro(7)
        };
    }
}
