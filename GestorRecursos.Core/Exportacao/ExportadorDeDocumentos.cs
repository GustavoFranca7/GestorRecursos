using System.Text;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Core.Exportacao;

/// <summary>Grava em disco todos os documentos anexados, em pastas por cliente.</summary>
/// <remarks>
/// Existe porque o backup semanal é de texto e não carrega os documentos: sem esta
/// rotina, o anexo só existe dentro da instância Oracle, que não tem restauração.
///
/// Nunca apaga nada — nem o que está no banco, nem o que já está na pasta.
///
/// São duas formas de rodar, e a diferença é o espaço:
/// <list type="bullet">
/// <item><see cref="ExportarAsync"/> — à mão, cria uma pasta datada própria. Serve para
/// levar uma cópia hoje, a um lugar qualquer, sem mexer no que já existe.</item>
/// <item><see cref="AtualizarEspelhoAsync"/> — toda semana, numa pasta só, gravando
/// apenas o que ainda não está lá. Uma cópia datada por semana encheria o Drive de
/// repetição: o mesmo documento, cinquenta vezes por ano.</item>
/// </list>
/// </remarks>
public static class ExportadorDeDocumentos
{
    private static readonly ArquivoDAO Anexos = new();

    // Nome de pasta e de arquivo tem limite no Windows. Cortar aqui evita que um
    // cliente de nome comprido faça a gravacao falhar la no fim do caminho.
    private const int LimiteDoNome = 60;
    private const int LimiteDoArquivo = 80;

    // O que ja foi gravado no espelho, por id de anexo. Sem isto, a unica forma de
    // saber se um documento ja esta la seria pelo nome - e dois anexos do mesmo
    // cliente podem ter o mesmo nome.
    private const string NomeDoIndice = "indice-de-documentos.txt";

    /// <summary>A fila do que será exportado, para a tela mostrar o tamanho antes de começar.</summary>
    public static Task<List<DocumentoParaExportar>> ListarAsync(
        CancellationToken cancelamento = default) => Anexos.ListarTudoAsync(cancelamento);

    /// <summary>Grava os documentos dentro de uma pasta nova, datada, em <paramref name="destino"/>.</summary>
    /// <returns>A pasta criada e o resultado.</returns>
    public static async Task<(string Pasta, ResultadoDaExportacao Resultado)> ExportarAsync(
        string destino,
        IReadOnlyList<DocumentoParaExportar> documentos,
        IProgress<AndamentoDaExportacao>? andamento = null,
        CancellationToken cancelamento = default)
    {
        var raiz = PastaNova(destino);
        Directory.CreateDirectory(raiz);

        var falhas = new List<string>();
        var gravados = 0;
        var bytes = 0L;
        var cancelada = false;

        for (var i = 0; i < documentos.Count; i++)
        {
            if (cancelamento.IsCancellationRequested)
            {
                cancelada = true;
                break;
            }

            var documento = documentos[i];
            andamento?.Report(new AndamentoDaExportacao(i, documentos.Count, documento.NomeArquivo));

            try
            {
                // Um documento por vez na memória: são até 10 MB cada, e carregar a
                // fila inteira para gravar um arquivo não traria nada em troca.
                var conteudo = await Anexos.LerConteudoAsync(documento.Id, cancelamento);

                if (conteudo is null)
                {
                    falhas.Add($"{documento.ClienteNome} / {documento.NomeArquivo}: "
                               + "o documento não está mais no banco.");
                    continue;
                }

                var pasta = Path.Combine(raiz, PastaDoCliente(documento), PastaDoTipo(documento.Tipo));
                Directory.CreateDirectory(pasta);

                var caminho = CaminhoLivre(pasta, documento.NomeArquivo);
                await File.WriteAllBytesAsync(caminho, conteudo, cancelamento);

                gravados++;
                bytes += conteudo.Length;
            }
            catch (OperationCanceledException)
            {
                cancelada = true;
                break;
            }
            catch (Exception excecao)
            {
                // Um documento que falha não derruba a exportação: o resto ainda sai,
                // e o que faltou fica nomeado no fim.
                falhas.Add($"{documento.ClienteNome} / {documento.NomeArquivo}: {excecao.Message}");
            }
        }

        andamento?.Report(new AndamentoDaExportacao(gravados, documentos.Count, string.Empty));

        var resultado = new ResultadoDaExportacao(gravados, bytes, falhas, cancelada);
        await EscreverResumoAsync(raiz, documentos.Count, resultado);

        return (raiz, resultado);
    }

    /// <summary>Põe na pasta os documentos que ainda não estão lá. É o que o backup semanal chama.</summary>
    /// <remarks>
    /// Incremental de propósito. Gravar tudo toda semana custaria o tamanho do acervo
    /// multiplicado pelas semanas, numa pasta sincronizada com a nuvem — e o conteúdo
    /// seria o mesmo. Aqui a segunda execução copia só o que entrou na semana.
    ///
    /// O que já saiu nunca é apagado nem regravado, inclusive quando o anexo some da
    /// ficha: um documento excluído por engano continua recuperável daqui.
    /// </remarks>
    public static async Task<ResultadoDoEspelho> AtualizarEspelhoAsync(
        string pasta,
        CancellationToken cancelamento = default)
    {
        Directory.CreateDirectory(pasta);

        var indice = await LerIndiceAsync(pasta);
        var documentos = await Anexos.ListarTudoAsync(cancelamento);

        var falhas = new List<string>();
        var novos = 0;
        var jaEstavam = 0;
        var sumiram = 0;
        var bytes = 0L;

        foreach (var documento in documentos)
        {
            cancelamento.ThrowIfCancellationRequested();

            // Conferir o arquivo, e não só o índice: alguém pode ter movido a pasta.
            if (indice.TryGetValue(documento.Id, out var ja) &&
                File.Exists(Path.Combine(pasta, ja)))
            {
                jaEstavam++;
                continue;
            }

            try
            {
                var conteudo = await Anexos.LerConteudoAsync(documento.Id, cancelamento);

                if (conteudo is null)
                {
                    sumiram++;
                    continue;
                }

                var destino = Path.Combine(
                    pasta, PastaDoCliente(documento), PastaDoTipo(documento.Tipo));
                Directory.CreateDirectory(destino);

                var caminho = CaminhoLivre(destino, documento.NomeArquivo);
                await File.WriteAllBytesAsync(caminho, conteudo, cancelamento);

                indice[documento.Id] = Path.GetRelativePath(pasta, caminho);
                novos++;
                bytes += conteudo.Length;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception excecao)
            {
                falhas.Add($"{documento.ClienteNome} / {documento.NomeArquivo}: {excecao.Message}");
            }
        }

        await GravarIndiceAsync(pasta, indice);

        var resultado = new ResultadoDoEspelho(novos, jaEstavam, sumiram, bytes, falhas);
        await EscreverResumoDoEspelhoAsync(pasta, indice.Count, resultado);

        return resultado;
    }

    /// <summary>O que já foi gravado aqui, por id de anexo. Vazio na primeira vez.</summary>
    private static async Task<Dictionary<int, string>> LerIndiceAsync(string pasta)
    {
        var caminho = Path.Combine(pasta, NomeDoIndice);
        var indice = new Dictionary<int, string>();

        if (!File.Exists(caminho))
        {
            return indice;
        }

        foreach (var linha in await File.ReadAllLinesAsync(caminho, Encoding.UTF8))
        {
            if (linha.StartsWith('#') || string.IsNullOrWhiteSpace(linha))
            {
                continue;
            }

            var partes = linha.Split('\t', 2);

            if (partes.Length == 2 && int.TryParse(partes[0], out var id))
            {
                indice[id] = partes[1];
            }
        }

        return indice;
    }

    private static async Task GravarIndiceAsync(string pasta, Dictionary<int, string> indice)
    {
        var texto = new StringBuilder()
            .AppendLine("# Indice do espelho de documentos do Gestor de Recursos.")
            .AppendLine("# id do anexo <TAB> caminho dentro desta pasta.")
            .AppendLine("# Apagar este arquivo faz a proxima execucao gravar tudo de novo,")
            .AppendLine("# ao lado do que ja esta aqui. Nada e perdido, mas duplica.");

        foreach (var (id, caminho) in indice.OrderBy(p => p.Key))
        {
            texto.AppendLine($"{id}\t{caminho}");
        }

        await File.WriteAllTextAsync(
            Path.Combine(pasta, NomeDoIndice), texto.ToString(), new UTF8Encoding(false));
    }

    private static async Task EscreverResumoDoEspelhoAsync(
        string pasta,
        int totalNoEspelho,
        ResultadoDoEspelho resultado)
    {
        var texto = new StringBuilder();

        texto.AppendLine("GESTOR DE RECURSOS - documentos dos clientes")
             .AppendLine($"Atualizado em {DateTime.Now:dd/MM/yyyy HH:mm}")
             .AppendLine()
             .AppendLine($"Documentos nesta pasta : {totalNoEspelho}")
             .AppendLine($"Novos nesta execucao   : {resultado.Novos}")
             .AppendLine($"Ja estavam aqui        : {resultado.JaEstavam}")
             .AppendLine()
             .AppendLine("Esta pasta acompanha o backup semanal. Cada execucao grava apenas")
             .AppendLine("o que ainda nao estava aqui, e nunca apaga nada - inclusive o")
             .AppendLine("documento que foi excluido da ficha depois de ter sido copiado.")
             .AppendLine()
             .AppendLine("Uma pasta por cliente, no formato \"id - NOME\", e dentro dela uma")
             .AppendLine("pasta por tipo de documento.")
             .AppendLine()
             .AppendLine("Esta pasta tem documento de cliente: CPF, RG, endereco, CNH.")
             .AppendLine("Nao deixe em pasta compartilhada nem em pendrive que circula.");

        if (resultado.Falhas.Count > 0)
        {
            texto.AppendLine()
                 .AppendLine($"{resultado.Falhas.Count} documento(s) nao sairam nesta execucao:");

            foreach (var falha in resultado.Falhas)
            {
                texto.AppendLine($"  {falha}");
            }
        }

        await File.WriteAllTextAsync(
            Path.Combine(pasta, "leia-me.txt"), texto.ToString(), new UTF8Encoding(false));
    }

    /// <summary>Uma pasta que ainda não existe, com a data da exportação no nome.</summary>
    private static string PastaNova(string destino)
    {
        var baseNome = $"Documentos dos clientes {DateTime.Now:yyyy-MM-dd}";
        var candidato = Path.Combine(destino, baseNome);

        for (var n = 2; Directory.Exists(candidato); n++)
        {
            candidato = Path.Combine(destino, $"{baseNome} ({n})");
        }

        return candidato;
    }

    /// <summary>"1042 - JOAO SILVA". O id na frente: dois clientes podem ter o mesmo nome.</summary>
    private static string PastaDoCliente(DocumentoParaExportar documento)
    {
        var nome = Encurtar(NomeSeguro(documento.ClienteNome), LimiteDoNome);
        return string.IsNullOrWhiteSpace(nome)
            ? documento.ClienteId.ToString()
            : $"{documento.ClienteId} - {nome}";
    }

    /// <summary>Do valor que o banco guarda: PROCURACAO vira "procuracao".</summary>
    /// <remarks>
    /// Derivado, não escrito à mão: tipo novo no enum ganha pasta sozinho, e não há
    /// uma segunda lista de nomes para esquecer de atualizar.
    /// </remarks>
    private static string PastaDoTipo(TipoArquivo tipo) =>
        tipo.Texto().Replace('_', ' ').ToLowerInvariant();

    /// <summary>O caminho do arquivo, com sufixo quando o nome já está ocupado.</summary>
    /// <remarks>
    /// Nunca sobrescreve. Dois anexos do mesmo cliente, do mesmo tipo, com o mesmo nome
    /// são possíveis — e a segunda via de uma CNH não pode apagar a primeira.
    /// </remarks>
    private static string CaminhoLivre(string pasta, string nomeArquivo)
    {
        var seguro = NomeSeguro(nomeArquivo);
        var extensao = Path.GetExtension(seguro);
        var semExtensao = Encurtar(Path.GetFileNameWithoutExtension(seguro), LimiteDoArquivo);

        if (string.IsNullOrWhiteSpace(semExtensao))
        {
            semExtensao = "documento";
        }

        var candidato = Path.Combine(pasta, semExtensao + extensao);

        for (var n = 2; File.Exists(candidato); n++)
        {
            candidato = Path.Combine(pasta, $"{semExtensao} ({n}){extensao}");
        }

        return candidato;
    }

    /// <summary>Tira do nome o que o sistema de arquivos não aceita.</summary>
    private static string NomeSeguro(string nome)
    {
        var invalidos = Path.GetInvalidFileNameChars();
        var limpo = new string(nome.Select(c => invalidos.Contains(c) ? '_' : c).ToArray());

        // Ponto e espaço no fim somem no Windows e deixam o nome diferente do pedido.
        return limpo.Trim().TrimEnd('.');
    }

    private static string Encurtar(string texto, int limite) =>
        texto.Length <= limite ? texto : texto[..limite].TrimEnd();

    /// <summary>Um resumo em texto dentro da própria pasta, para ela se explicar sozinha.</summary>
    /// <remarks>
    /// A pasta pode ser aberta daqui a dois anos, por outra pessoa, num computador que
    /// não tem o sistema. O resumo diz o que ela é e o que ficou de fora.
    /// </remarks>
    private static async Task EscreverResumoAsync(
        string raiz,
        int total,
        ResultadoDaExportacao resultado)
    {
        var texto = new StringBuilder();

        texto.AppendLine("GESTOR DE RECURSOS - documentos dos clientes")
             .AppendLine($"Exportado em {DateTime.Now:dd/MM/yyyy HH:mm}")
             .AppendLine()
             .AppendLine($"Documentos no sistema : {total}")
             .AppendLine($"Gravados aqui         : {resultado.Gravados}")
             .AppendLine()
             .AppendLine("Uma pasta por cliente, no formato \"id - NOME\", e dentro dela uma")
             .AppendLine("pasta por tipo de documento. Nada foi apagado do sistema.")
             .AppendLine()
             .AppendLine("Esta pasta tem documento de cliente: CPF, RG, endereco, CNH.")
             .AppendLine("Nao deixe em pasta compartilhada nem em pendrive que circula.");

        if (resultado.Cancelada)
        {
            texto.AppendLine()
                 .AppendLine("ATENCAO: a exportacao foi interrompida antes do fim.")
                 .AppendLine("O que esta aqui esta inteiro, mas falta documento.");
        }

        if (resultado.Falhas.Count > 0)
        {
            texto.AppendLine()
                 .AppendLine($"{resultado.Falhas.Count} documento(s) nao sairam:");

            foreach (var falha in resultado.Falhas)
            {
                texto.AppendLine($"  {falha}");
            }
        }

        // Sem token: o resumo sai inclusive quando cancelaram, senao a pasta nao diz
        // que esta incompleta.
        await File.WriteAllTextAsync(
            Path.Combine(raiz, "leia-me.txt"), texto.ToString(), new UTF8Encoding(false));
    }
}
