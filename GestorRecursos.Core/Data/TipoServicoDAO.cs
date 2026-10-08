using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: a tabela de preços.
public class TipoServicoDAO
{
    public async Task<List<TipoServico>> ListarAsync(bool apenasAtivos = true, CancellationToken cancelamento = default)
    {
        var filtro = apenasAtivos ? "WHERE ATIVO = 'S'" : string.Empty;
        var sql = $"""
            SELECT ID, NOME, TEM_INSTANCIAS, VALOR_TABELA, ATIVO
              FROM TIPOS_SERVICO {filtro}
             ORDER BY NOME
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);

        var lista = new List<TipoServico>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(Ler(reader));
        }

        return lista;
    }

    public async Task<TipoServico?> BuscarPorIdAsync(int id, CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT ID, NOME, TEM_INSTANCIAS, VALOR_TABELA, ATIVO
              FROM TIPOS_SERVICO WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", id);

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        return await reader.ReadAsync(cancelamento) ? Ler(reader) : null;
    }

    /// <summary>Muda o preço de tabela de um serviço.</summary>
    /// <remarks>
    /// Alterar aqui não mexe no que já foi contratado: a contratação guarda o
    /// valor fechado na época. Preço de tabela é referência do dia, não histórico.
    /// </remarks>
    public async Task<bool> AtualizarValorAsync(int id, decimal novoValor, CancellationToken cancelamento = default)
    {
        const string sql = "UPDATE TIPOS_SERVICO SET VALOR_TABELA = :valor WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("valor", novoValor).Param("id", id);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    private static TipoServico Ler(OracleDataReader reader) => new()
    {
        Id = reader.Inteiro(0),
        Nome = reader.Texto(1),
        TemInstancias = reader.Booleano(2),
        ValorTabela = reader.Decimal(3),
        Ativo = reader.Booleano(4)
    };
}
