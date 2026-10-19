using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: a tabela de preços.
public class TipoServicoDAO
{
    // A contagem de etapas vem junto: é ela que diz, já na lista de escolha, que o
    // serviço tem um roteiro a cumprir depois de contratado.
    private const string Colunas = """
        T.ID, T.NOME, T.TEM_INSTANCIAS, T.VALOR_TABELA, T.ATIVO, T.FLUXO,
        (SELECT COUNT(*) FROM ETAPAS_MODELO E WHERE E.TIPO_SERVICO_ID = T.ID)
        """;

    public async Task<List<TipoServico>> ListarAsync(bool apenasAtivos = true, CancellationToken cancelamento = default)
    {
        var filtro = apenasAtivos ? "WHERE T.ATIVO = 'S'" : string.Empty;
        var sql = $"""
            SELECT {Colunas}
              FROM TIPOS_SERVICO T {filtro}
             ORDER BY T.NOME
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
        var sql = $"SELECT {Colunas} FROM TIPOS_SERVICO T WHERE T.ID = :id";

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
        Ativo = reader.Booleano(4),
        Fluxo = ValorBanco.De<FluxoServico>(reader.Texto(5)),
        QuantidadeEtapas = reader.Inteiro(6)
    };
}
