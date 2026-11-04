using System.IO.Compression;
using GestorRecursos.Core;
using GestorRecursos.Core.Exportacao;

namespace GestorRecursos.Backup;

// Camada Backup: o executavel que o Agendador de Tarefas chama uma vez por semana.
//
// Existe porque o Always Free nao tem restauracao: este arquivo nao e a segunda
// linha de defesa, e a unica. Ver "Documentacao/1 Projeto/Rotina de backup.md".
internal static class Program
{
    private static async Task<int> Main(string[] argumentos)
    {
        var inicio = DateTime.Now;

        try
        {
            var pasta = Destino(argumentos);
            Directory.CreateDirectory(pasta);

            var nome = $"backup-{inicio:yyyy-MM-dd}";
            var sql = Path.Combine(pasta, $"{nome}.sql");
            var zip = Path.Combine(pasta, $"{nome}.zip");

            Dizer($"Gestor de Recursos - backup de {inicio:dd/MM/yyyy HH:mm}");
            Dizer($"Ambiente: {Configuracoes.Ambiente ?? "producao"}");
            Dizer($"Destino : {pasta}");

            var resumo = await new Exportador().ExportarAsync(sql);

            Dizer($"Banco   : {resumo.Banco}");
            Dizer($"Esquema : {resumo.Esquema}");
            Dizer($"Tabelas : {resumo.Tabelas}");
            Dizer($"Linhas  : {resumo.Linhas}");
            Dizer($"Arquivo : {Tamanho(resumo.Bytes)}");

            foreach (var ignorada in resumo.Ignoradas)
            {
                Dizer($"  fora do arquivo: {ignorada}");
            }

            foreach (var tabela in resumo.NaoRestauraveis)
            {
                Dizer($"  NAO RESTAURAVEL: {tabela} - vao comentadas, so como registro");
            }

            Compactar(sql, zip);
            var comprimido = new FileInfo(zip).Length;

            // O .sql sai depois de compactado: deixar os dois dobraria o espaco e, pior,
            // deixaria a versao nao compactada - a que qualquer programa abre - solta na
            // pasta sincronizada.
            File.Delete(sql);

            Dizer($"Compactado: {Tamanho(comprimido)} ({Percentual(comprimido, resumo.Bytes)} do original)");

            var documentos = await EspelharDocumentosAsync(pasta);

            // Linha vazia nao e enfeite: o Agendador acumula a saida de todas as
            // execucoes num log so, e sem separador elas viram um bloco ilegivel.
            Dizer($"Concluido em {(DateTime.Now - inicio).TotalSeconds:F1}s");
            Dizer(string.Empty);

            // Documento que nao saiu precisa aparecer como falha no Agendador. O .zip do
            // texto esta gravado de qualquer jeito, e a mensagem acima diz isso.
            return documentos.Falhas.Count > 0 ? 1 : 0;
        }
        catch (Exception excecao)
        {
            // Codigo de saida diferente de zero e o que o Agendador entende como falha.
            // Sem isto, um backup que nunca rodou apareceria como "concluido com exito".
            Dizer($"FALHOU: {Motivo(excecao)}");
            Dizer(string.Empty);
            return 1;
        }
    }

    /// <summary>Copia para o disco os documentos que ainda não estão lá.</summary>
    /// <remarks>
    /// O .sql do texto não carrega BLOB, então sem este passo os anexos existiriam só
    /// dentro da instância Oracle — que não tem restauração.
    ///
    /// A pasta é uma só, e cada semana acrescenta o que entrou: uma cópia datada por
    /// semana repetiria o acervo inteiro cinquenta vezes por ano dentro do Drive.
    /// </remarks>
    private static async Task<ResultadoDoEspelho> EspelharDocumentosAsync(string pasta)
    {
        var espelho = Path.Combine(pasta, "Documentos dos clientes");
        var resultado = await ExportadorDeDocumentos.AtualizarEspelhoAsync(espelho);

        Dizer($"Documentos: {resultado.Novos} novo(s), "
              + $"{resultado.JaEstavam} ja estavam, {Tamanho(resultado.Bytes)} copiados");

        if (resultado.Sumiram > 0)
        {
            Dizer($"  {resultado.Sumiram} anexo(s) sairam da ficha durante a copia "
                  + "- a versao anterior continua na pasta");
        }

        foreach (var falha in resultado.Falhas)
        {
            Dizer($"  NAO COPIADO: {falha}");
        }

        return resultado;
    }

    /// <summary>Onde o arquivo vai parar: argumento, configuração, ou a pasta do programa.</summary>
    /// <remarks>
    /// A pasta certa é uma sincronizada com Drive ou OneDrive: assim o backup sai do
    /// computador sozinho, e um disco que morre não leva a única cópia com ele.
    /// </remarks>
    private static string Destino(string[] argumentos)
    {
        if (argumentos.Length > 0 && !string.IsNullOrWhiteSpace(argumentos[0]))
        {
            return argumentos[0];
        }

        var configurada = Configuracoes.Atual["Backup:Pasta"];

        return string.IsNullOrWhiteSpace(configurada)
            ? Path.Combine(AppContext.BaseDirectory, "backups")
            : configurada;
    }

    private static void Compactar(string sql, string zip)
    {
        if (File.Exists(zip))
        {
            File.Delete(zip);
        }

        using var pacote = ZipFile.Open(zip, ZipArchiveMode.Create);
        pacote.CreateEntryFromFile(sql, Path.GetFileName(sql), CompressionLevel.SmallestSize);
    }

    private static string Tamanho(long bytes) =>
        bytes < 1024 * 1024
            ? $"{bytes / 1024.0:F1} KB"
            : $"{bytes / 1024.0 / 1024.0:F1} MB";

    private static string Percentual(long parte, long total) =>
        total == 0 ? "?" : $"{parte * 100.0 / total:F0}%";

    // A excecao de fora costuma dizer pouco ("Call timed out"); o que explica esta na
    // de dentro. Mesma logica do EnvioDeEmail.
    private static string Motivo(Exception excecao)
    {
        var partes = new List<string>();

        for (var atual = excecao; atual is not null; atual = atual.InnerException)
        {
            partes.Add(atual.Message);
        }

        return string.Join(" - ", partes.Distinct());
    }

    private static void Dizer(string texto) => Console.WriteLine(texto);
}
