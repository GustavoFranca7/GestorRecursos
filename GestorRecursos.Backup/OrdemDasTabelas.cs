using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Backup;

/// <summary>Descobre em que ordem as tabelas podem ser inseridas.</summary>
/// <remarks>
/// A ordem sai do próprio banco, lida das chaves estrangeiras, e não de uma lista
/// escrita à mão. Lista à mão envelhece em silêncio: a tabela nova entraria no sistema
/// e ficaria fora do backup sem ninguém notar — e isso só apareceria na restauração,
/// no pior dia possível.
/// </remarks>
internal static class OrdemDasTabelas
{
    // Tabelas que o Database actions cria para guardar o historico de comandos
    // digitados na tela de SQL. Chegam a pesar mais que o sistema inteiro e nao sao
    // dado nosso.
    private const string Alheias = "DBTOOLS%";

    /// <summary>As tabelas do esquema, pai antes de filho.</summary>
    public static async Task<List<string>> DescobrirAsync(
        OracleConnection conexao,
        string dono,
        CancellationToken cancelamento = default)
    {
        var tabelas = await LerTabelasAsync(conexao, dono, cancelamento);
        var dependencias = await LerDependenciasAsync(conexao, dono, tabelas, cancelamento);

        return Ordenar(tabelas, dependencias);
    }

    private static async Task<List<string>> LerTabelasAsync(
        OracleConnection conexao,
        string dono,
        CancellationToken cancelamento)
    {
        // ALL_TABLES com o dono explícito, e não USER_TABLES. Nas estações o usuário do
        // banco não é o dono do esquema: USER_TABLES devolveria zero tabela, e o backup
        // sairia vazio sem reclamar.
        //
        // Só tabela de verdade: view não guarda dado, e tabela temporária não
        // sobrevive à sessão, então não há o que salvar nas duas.
        const string sql = """
            SELECT TABLE_NAME
              FROM ALL_TABLES
             WHERE OWNER = :dono
               AND TABLE_NAME NOT LIKE :alheias
               AND TEMPORARY = 'N'
             ORDER BY TABLE_NAME
            """;

        await using var cmd = new OracleCommand(sql, conexao) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("dono", dono));
        cmd.Parameters.Add(new OracleParameter("alheias", Alheias));

        var nomes = new List<string>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);

        while (await reader.ReadAsync(cancelamento))
        {
            nomes.Add(reader.GetString(0));
        }

        return nomes;
    }

    /// <summary>Para cada tabela, de quais outras ela depende.</summary>
    private static async Task<Dictionary<string, HashSet<string>>> LerDependenciasAsync(
        OracleConnection conexao,
        string dono,
        List<string> tabelas,
        CancellationToken cancelamento)
    {
        // O par (dono, nome) identifica a restrição: só o nome bastaria se o usuário
        // fosse o dono, que é justamente o que não se pode supor.
        const string sql = """
            SELECT filha.TABLE_NAME AS FILHA,
                   pai.TABLE_NAME   AS PAI
              FROM ALL_CONSTRAINTS filha
              JOIN ALL_CONSTRAINTS pai
                ON pai.OWNER = filha.R_OWNER
               AND pai.CONSTRAINT_NAME = filha.R_CONSTRAINT_NAME
             WHERE filha.CONSTRAINT_TYPE = 'R'
               AND filha.OWNER = :dono
            """;

        var mapa = tabelas.ToDictionary(t => t, _ => new HashSet<string>());

        await using var cmd = new OracleCommand(sql, conexao) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("dono", dono));
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);

        while (await reader.ReadAsync(cancelamento))
        {
            var filha = reader.GetString(0);
            var pai = reader.GetString(1);

            // Auto-referência não ordena tabela, ordena linha dentro dela — e isso o
            // backup resolve de outro jeito (ver Exportador). Aqui ela sairia como
            // ciclo e travaria tudo.
            if (filha == pai || !mapa.ContainsKey(filha) || !mapa.ContainsKey(pai))
            {
                continue;
            }

            mapa[filha].Add(pai);
        }

        return mapa;
    }

    /// <summary>Ordenação topológica: ninguém entra antes de quem ele aponta.</summary>
    /// <remarks>
    /// Empate resolvido por nome, de propósito: assim dois backups do mesmo banco saem
    /// com as tabelas na mesma ordem, e comparar dois arquivos mostra o que mudou nos
    /// dados em vez de ruído de ordenação.
    /// </remarks>
    private static List<string> Ordenar(
        List<string> tabelas,
        Dictionary<string, HashSet<string>> dependencias)
    {
        var ordenadas = new List<string>();
        var dentro = new HashSet<string>();

        while (ordenadas.Count < tabelas.Count)
        {
            var prontas = tabelas
                .Where(t => !dentro.Contains(t) && dependencias[t].All(dentro.Contains))
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();

            // Nenhuma pronta e ainda sobrou tabela: há um ciclo de chaves estrangeiras.
            // Em vez de travar, entrega o que falta em ordem alfabética e deixa o aviso
            // para quem lê — a restauração vai precisar de atenção nessas.
            if (prontas.Count == 0)
            {
                ordenadas.AddRange(tabelas
                    .Where(t => !dentro.Contains(t))
                    .OrderBy(t => t, StringComparer.Ordinal));
                break;
            }

            ordenadas.AddRange(prontas);
            foreach (var pronta in prontas)
            {
                dentro.Add(pronta);
            }
        }

        return ordenadas;
    }
}
