using System.Text;
using GestorRecursos.Core.Data;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Backup;

/// <summary>O que o backup encontrou e escreveu.</summary>
internal sealed record Resumo(
    string Banco,
    string Esquema,
    int Tabelas,
    int Linhas,
    long Bytes,
    List<string> Ignoradas,
    List<string> NaoRestauraveis);

/// <summary>Lê o banco inteiro e escreve um arquivo .sql que o recria.</summary>
/// <remarks>
/// Só texto, número e data. Coluna grande (BLOB, CLOB) fica de fora de propósito: é o
/// que mantém o arquivo em dezenas de KB e permite rodar toda semana sem pensar. Os
/// documentos saem pela exportação em massa, que é outra rotina.
/// </remarks>
internal sealed class Exportador
{
    // Tipos que nao cabem num arquivo de texto. Ficam de fora com aviso, nunca em
    // silencio - quem restaura precisa saber que falta algo e de onde vem.
    private static readonly string[] TiposGrandes = ["BLOB", "CLOB", "NCLOB", "BFILE", "LONG", "RAW", "LONG RAW"];

    public async Task<Resumo> ExportarAsync(string destino, CancellationToken cancelamento = default)
    {
        await using var conexao = await Database.AbrirAsync(cancelamento);

        // Primeira coisa que vai para o arquivo e para a tela: em que banco estamos.
        // Ja houve medicao feita contra o banco de teste pensando que era producao, e
        // o que evitou o engano foi exatamente perguntar isto em vez de supor.
        var banco = await EscalarAsync(conexao, "SELECT SYS_CONTEXT('USERENV','DB_NAME') FROM DUAL", cancelamento);

        // De quem são as tabelas. Nas estações o usuário do banco não é o dono do esquema,
        // então o dono vem daqui e não de quem conectou.
        var esquema = await EscalarAsync(
            conexao, "SELECT SYS_CONTEXT('USERENV','CURRENT_SCHEMA') FROM DUAL", cancelamento);

        var tabelas = await OrdemDasTabelas.DescobrirAsync(conexao, esquema, cancelamento);
        var colunas = await LerColunasAsync(conexao, esquema, cancelamento);

        // Zero tabela não é um banco vazio, é uma busca no lugar errado. Falhar aqui é o
        // que impede um arquivo de 1 KB de passar por backup até o dia em que for preciso.
        if (tabelas.Count == 0)
        {
            throw new InvalidOperationException(
                $"Nenhuma tabela encontrada no esquema {esquema} do banco {banco}. "
                + "Confira a configuração Oracle:Esquema.");
        }

        var ignoradas = new List<string>();
        var naoRestauraveis = new List<string>();
        var linhas = 0;

        await using var arquivo = new StreamWriter(destino, append: false, new UTF8Encoding(false));
        await EscreverCabecalhoAsync(arquivo, banco, esquema, tabelas.Count);

        foreach (var tabela in tabelas)
        {
            if (!colunas.TryGetValue(tabela, out var todas))
            {
                continue;
            }

            var gravaveis = todas.Where(c => !TiposGrandes.Contains(c.Tipo)).ToList();
            var fora = todas.Where(c => TiposGrandes.Contains(c.Tipo)).ToList();

            foreach (var coluna in fora)
            {
                ignoradas.Add($"{tabela}.{coluna.Nome} ({coluna.Tipo})");
            }

            if (gravaveis.Count == 0)
            {
                continue;
            }

            // Coluna grande que e obrigatoria impede o INSERT: ele rodaria sem ela e
            // o banco recusaria a linha. Entao a linha vai comentada - o registro de
            // que o documento existiu se mantem, sem produzir um restauro que falha
            // nem um anexo que abre vazio.
            var impedem = fora.Where(c => c.Obrigatoria).ToList();

            var escritas = await EscreverTabelaAsync(
                arquivo, conexao, esquema, tabela, gravaveis, fora, impedem, cancelamento);

            if (impedem.Count > 0)
            {
                if (escritas > 0)
                {
                    naoRestauraveis.Add($"{tabela}: {escritas} linha(s)");
                }
            }
            else
            {
                linhas += escritas;
            }
        }

        await EscreverRodapeAsync(arquivo, linhas, ignoradas, naoRestauraveis);
        await arquivo.FlushAsync(cancelamento);
        arquivo.Close();

        return new Resumo(
            banco, esquema, tabelas.Count, linhas, new FileInfo(destino).Length,
            ignoradas, naoRestauraveis);
    }

    private async Task<int> EscreverTabelaAsync(
        StreamWriter arquivo,
        OracleConnection conexao,
        string esquema,
        string tabela,
        List<Coluna> gravaveis,
        List<Coluna> fora,
        List<Coluna> impedem,
        CancellationToken cancelamento)
    {
        // Linha que nao pode ser inserida vai como comentario, nunca como comando.
        var prefixo = impedem.Count > 0 ? "-- " : string.Empty;

        var nomes = string.Join(", ", gravaveis.Select(c => c.Nome));

        // Ordena pelo ID quando existe: dentro de uma tabela que aponta para si mesma,
        // o pai tem ID menor que o filho, porque foi cadastrado antes. Resolve a
        // auto-referência que a ordenação de tabelas não alcança.
        var ordem = gravaveis.Any(c => c.Nome == "ID") ? " ORDER BY ID" : string.Empty;

        // A leitura vai com o dono na frente; o INSERT gerado, não. Quem restaura pode ser
        // outro usuário, e um nome de esquema gravado no arquivo o amarraria a este banco.
        await using var cmd = new OracleCommand($"SELECT {nomes} FROM {esquema}.{tabela}{ordem}", conexao)
        {
            BindByName = true
        };

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);

        var escritas = 0;
        var valores = new object?[gravaveis.Count];

        while (await reader.ReadAsync(cancelamento))
        {
            if (escritas == 0)
            {
                await arquivo.WriteLineAsync();
                await arquivo.WriteLineAsync($"-- {tabela}");

                foreach (var coluna in fora)
                {
                    await arquivo.WriteLineAsync(
                        $"-- ATENCAO: a coluna {coluna.Nome} ({coluna.Tipo}) nao vem neste arquivo.");
                }

                if (impedem.Count > 0)
                {
                    var nomesImpedem = string.Join(", ", impedem.Select(c => c.Nome));

                    await arquivo.WriteLineAsync(
                        $"-- As linhas abaixo estao COMENTADAS: {nomesImpedem} e obrigatoria e nao");
                    await arquivo.WriteLineAsync(
                        "-- vem aqui, entao o INSERT seria recusado pelo banco. Ficam como registro");
                    await arquivo.WriteLineAsync(
                        "-- do que existia. Os documentos saem pela exportacao em massa.");
                }
            }

            for (var i = 0; i < gravaveis.Count; i++)
            {
                valores[i] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }

            await arquivo.WriteLineAsync(
                $"{prefixo}INSERT INTO {tabela} ({nomes}) VALUES ({string.Join(", ", valores.Select(ValorSql.De))});");

            escritas++;
        }

        if (escritas > 0 && impedem.Count == 0)
        {
            await arquivo.WriteLineAsync("COMMIT;");
        }

        return escritas;
    }

    private sealed record Coluna(string Nome, string Tipo, bool Obrigatoria);

    /// <summary>Os nomes e tipos de coluna, lidos do banco.</summary>
    /// <remarks>
    /// Lidos, não escritos à mão: coluna nova entra no backup sozinha, e uma lista
    /// desatualizada nunca chega a existir.
    /// </remarks>
    private static async Task<Dictionary<string, List<Coluna>>> LerColunasAsync(
        OracleConnection conexao,
        string esquema,
        CancellationToken cancelamento)
    {
        const string sql = """
            SELECT TABLE_NAME, COLUMN_NAME, DATA_TYPE, NULLABLE
              FROM ALL_TAB_COLUMNS
             WHERE OWNER = :dono
             ORDER BY TABLE_NAME, COLUMN_ID
            """;

        var mapa = new Dictionary<string, List<Coluna>>();

        await using var cmd = new OracleCommand(sql, conexao) { BindByName = true };
        cmd.Parameters.Add(new OracleParameter("dono", esquema));
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);

        while (await reader.ReadAsync(cancelamento))
        {
            var tabela = reader.GetString(0);

            if (!mapa.TryGetValue(tabela, out var lista))
            {
                lista = [];
                mapa[tabela] = lista;
            }

            lista.Add(new Coluna(
                reader.GetString(1), reader.GetString(2), reader.GetString(3) == "N"));
        }

        return mapa;
    }

    private static async Task<string> EscalarAsync(
        OracleConnection conexao,
        string sql,
        CancellationToken cancelamento)
    {
        await using var cmd = new OracleCommand(sql, conexao) { BindByName = true };
        var valor = await cmd.ExecuteScalarAsync(cancelamento);
        return valor?.ToString() ?? "?";
    }

    private static async Task EscreverCabecalhoAsync(
        StreamWriter arquivo,
        string banco,
        string esquema,
        int tabelas)
    {
        await arquivo.WriteLineAsync($"""
            -- =====================================================================
            -- GESTOR DE RECURSOS - backup dos dados
            -- =====================================================================
            -- Banco   : {banco}
            -- Esquema : {esquema}
            -- Gerado  : {DateTime.Now:dd/MM/yyyy HH:mm:ss}
            -- Tabelas : {tabelas}
            --
            -- As tabelas estao na ordem em que podem ser inseridas: pai antes de
            -- filho. O arquivo roda de cima para baixo, sem desligar restricao.
            --
            -- Os IDs vao explicitos. Depois de restaurar, reacerte o contador de
            -- cada tabela, ou o primeiro cadastro novo colide:
            --
            --     ALTER TABLE CLIENTES MODIFY
            --         (ID GENERATED BY DEFAULT ON NULL AS IDENTITY (START WITH LIMIT VALUE));
            --
            -- O passo a passo completo da restauracao esta em
            -- "Documentacao/1 Projeto/Rotina de backup.md".
            -- =====================================================================
            """);
    }

    private static async Task EscreverRodapeAsync(
        StreamWriter arquivo,
        int linhas,
        List<string> ignoradas,
        List<string> naoRestauraveis)
    {
        await arquivo.WriteLineAsync();
        await arquivo.WriteLineAsync("-- ---------------------------------------------------------------------");
        await arquivo.WriteLineAsync($"-- Fim. {linhas} linhas.");

        if (naoRestauraveis.Count > 0)
        {
            await arquivo.WriteLineAsync("-- Alem delas, estas ficaram COMENTADAS e nao voltam ao restaurar:");

            foreach (var tabela in naoRestauraveis)
            {
                await arquivo.WriteLineAsync($"--   {tabela}");
            }
        }

        if (ignoradas.Count == 0)
        {
            await arquivo.WriteLineAsync("-- Nenhuma coluna ficou de fora.");
            return;
        }

        await arquivo.WriteLineAsync($"-- {ignoradas.Count} coluna(s) ficaram de fora, por serem grandes:");

        foreach (var ignorada in ignoradas)
        {
            await arquivo.WriteLineAsync($"--   {ignorada}");
        }

        await arquivo.WriteLineAsync("-- Elas saem pela exportacao em massa dos documentos, nao por aqui.");
    }
}
