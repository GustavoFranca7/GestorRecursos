using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: a agenda que substitui a planilha de produção diária.
public class AgendaDAO
{
    private const string Colunas = """
        A.ID, A.DATA_AGENDA, A.HORA, A.TIPO, A.TITULO, A.CLIENTE_ID,
        A.INSTANCIA_ID, A.CONCLUIDO, A.RECORRENCIA, A.OBSERVACAO
        """;

    /// <summary>Compromissos de um período, com o nome do cliente quando houver.</summary>
    /// <remarks>
    /// A visão padrão dela é o mês inteiro: "às vezes tem 1 dia com 15 recursos,
    /// e assim consigo ter uma noção futura melhor".
    /// </remarks>
    public async Task<List<Compromisso>> ListarPeriodoAsync(
        DateTime inicio,
        DateTime fim,
        CancellationToken cancelamento = default)
    {
        var sql = $"""
            SELECT {Colunas}, C.NOME
              FROM AGENDA A
              LEFT JOIN CLIENTES C ON C.ID = A.CLIENTE_ID
             WHERE A.DATA_AGENDA BETWEEN :inicio AND :fim
             ORDER BY A.DATA_AGENDA, A.HORA NULLS FIRST, A.ID
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("inicio", inicio.Date).Param("fim", fim.Date);

        var lista = new List<Compromisso>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            var compromisso = Ler(reader);
            compromisso.ClienteNome = reader.TextoOuNulo(10);
            lista.Add(compromisso);
        }

        return lista;
    }

    public async Task<List<Compromisso>> ListarDoMesAsync(int ano, int mes, CancellationToken cancelamento = default)
    {
        var inicio = new DateTime(ano, mes, 1);
        return await ListarPeriodoAsync(inicio, inicio.AddMonths(1).AddDays(-1), cancelamento);
    }

    public async Task<int> CriarAsync(Compromisso compromisso, CancellationToken cancelamento = default)
    {
        const string sql = """
            INSERT INTO AGENDA
                (DATA_AGENDA, HORA, TIPO, TITULO, CLIENTE_ID, INSTANCIA_ID,
                 CONCLUIDO, RECORRENCIA, OBSERVACAO)
            VALUES
                (:data, :hora, :tipo, :titulo, :clienteId, :instanciaId,
                 :concluido, :recorrencia, :observacao)
            RETURNING ID INTO :novoId
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("data", compromisso.Data.Date)
           .Param("hora", compromisso.Hora)
           .Param("tipo", compromisso.Tipo.Texto())
           .Param("titulo", compromisso.Titulo)
           .Param("clienteId", compromisso.ClienteId)
           .Param("instanciaId", compromisso.InstanciaId)
           .Param("concluido", compromisso.Concluido.ParaSN())
           .Param("recorrencia", compromisso.Recorrencia.Texto())
           .Param("observacao", compromisso.Observacao);

        var pId = new OracleParameter("novoId", OracleDbType.Int32)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        cmd.Parameters.Add(pId);

        await cmd.ExecuteNonQueryAsync(cancelamento);
        compromisso.Id = Convert.ToInt32(pId.Value.ToString());
        return compromisso.Id;
    }

    public async Task<bool> ConcluirAsync(int id, bool concluido = true, CancellationToken cancelamento = default)
    {
        const string sql = "UPDATE AGENDA SET CONCLUIDO = :concluido WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("concluido", concluido.ParaSN()).Param("id", id);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    public async Task<bool> ExcluirAsync(int id, CancellationToken cancelamento = default)
    {
        const string sql = "DELETE FROM AGENDA WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", id);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    // ---------------------------------------------------------------------
    // Diário do dia
    // ---------------------------------------------------------------------

    /// <summary>Grava a anotação do dia, criando ou substituindo a que existir.</summary>
    public async Task GravarAnotacaoAsync(DateTime dia, string texto, CancellationToken cancelamento = default)
    {
        const string sql = """
            MERGE INTO DIARIO_DIA d
            USING (SELECT :dia AS DATA_DIA FROM DUAL) s
               ON (d.DATA_DIA = s.DATA_DIA)
            WHEN MATCHED THEN UPDATE SET ANOTACAO = :texto
            WHEN NOT MATCHED THEN INSERT (DATA_DIA, ANOTACAO) VALUES (:dia, :texto)
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("dia", dia.Date).Param("texto", texto);

        await cmd.ExecuteNonQueryAsync(cancelamento);
    }

    public async Task<AnotacaoDiaria?> BuscarAnotacaoAsync(DateTime dia, CancellationToken cancelamento = default)
    {
        const string sql = "SELECT ID, DATA_DIA, ANOTACAO FROM DIARIO_DIA WHERE DATA_DIA = :dia";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("dia", dia.Date);

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        if (!await reader.ReadAsync(cancelamento))
        {
            return null;
        }

        return new AnotacaoDiaria
        {
            Id = reader.Inteiro(0),
            Data = reader.Data(1),
            Anotacao = reader.Texto(2)
        };
    }

    private static Compromisso Ler(OracleDataReader reader) => new()
    {
        Id = reader.Inteiro(0),
        Data = reader.Data(1),
        Hora = reader.TextoOuNulo(2),
        Tipo = ValorBanco.De<TipoCompromisso>(reader.Texto(3)),
        Titulo = reader.Texto(4),
        ClienteId = reader.InteiroOuNulo(5),
        InstanciaId = reader.InteiroOuNulo(6),
        Concluido = reader.Booleano(7),
        Recorrencia = ValorBanco.De<Recorrencia>(reader.Texto(8)),
        Observacao = reader.TextoOuNulo(9)
    };
}
