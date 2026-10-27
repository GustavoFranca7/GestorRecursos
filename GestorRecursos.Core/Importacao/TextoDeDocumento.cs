using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace GestorRecursos.Core.Importacao;

/// <summary>Tira o texto de um .docx ou de um .pdf.</summary>
/// <remarks>
/// Não é um leitor de documentos de uso geral: existe para ler as procurações antigas do
/// escritório, que são Word exportado ou salvo direto. Fora desse recorte ele devolve
/// nulo em vez de arriscar um texto meia-boca — importar lixo é pior que não importar.
/// </remarks>
/// <param name="Texto">Nulo quando não deu para ler.</param>
/// <param name="Motivo">Nulo quando deu certo. Dito para quem vai resolver.</param>
public sealed record TextoLido(string? Texto, string? Motivo)
{
    public static TextoLido Falhou(string motivo) => new(null, motivo);
}

public static class TextoDeDocumento
{
    public static readonly string[] ExtensoesAceitas = [".docx", ".pdf"];

    /// <summary>O texto do arquivo, ou o motivo de não ter dado.</summary>
    public static TextoLido Ler(string caminho)
    {
        try
        {
            var texto = Path.GetExtension(caminho).ToLowerInvariant() switch
            {
                ".docx" => DoDocx(caminho),
                ".pdf" => DoPdf(caminho),
                _ => null
            };

            if (string.IsNullOrWhiteSpace(texto))
            {
                return TextoLido.Falhou(
                    "Não há texto para ler. O arquivo pode estar corrompido, protegido por "
                    + "senha, ou ser uma digitalização — imagem de papel não tem texto dentro.");
            }

            // O Word espalha espaço inquebrável pelo texto, inclusive depois de
            // "OUTORGADO:". Quem procura o marcador não precisa saber disso.
            return new TextoLido(
                texto.Replace(' ', ' ').Replace(' ', ' ').Replace(' ', ' '),
                null);
        }
        catch (IOException)
        {
            // Acontece de verdade: documento aberto no Word enquanto a pasta é lida.
            return TextoLido.Falhou(
                "O arquivo está sendo usado por outro programa. Feche-o e leia a pasta de novo.");
        }
        catch (UnauthorizedAccessException)
        {
            return TextoLido.Falhou("Sem permissão para abrir este arquivo.");
        }
        catch (Exception excecao)
        {
            return TextoLido.Falhou($"Não foi possível abrir: {excecao.Message}");
        }
    }

    /// <summary>Abre para leitura sem exigir exclusividade.</summary>
    /// <remarks>
    /// <see cref="ZipFile.OpenRead"/> e <see cref="File.ReadAllBytes"/> pedem que mais
    /// ninguém esteja escrevendo, e o Word mantém aberto o documento que está na tela.
    /// Na prática é o caso comum: quem confere a procuração a tem aberta. Declarar que
    /// outros podem escrever faz a leitura passar.
    /// </remarks>
    private static FileStream AbrirCompartilhado(string caminho) =>
        new(caminho, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

    // ---------------------------------------------------------------- docx

    /// <summary>Um .docx é um zip; o texto mora em word/document.xml.</summary>
    private static string? DoDocx(string caminho)
    {
        using var fluxo = AbrirCompartilhado(caminho);
        using var zip = new ZipArchive(fluxo, ZipArchiveMode.Read);

        // O padrão manda barra normal, mas nem todo gerador obedece.
        var documento = zip.Entries.FirstOrDefault(e =>
            e.FullName.Replace('\\', '/').Equals("word/document.xml", StringComparison.OrdinalIgnoreCase));

        if (documento is null) return null;

        using var leitor = new StreamReader(documento.Open(), Encoding.UTF8);
        var xml = leitor.ReadToEnd();

        // Fim de parágrafo e quebra viram linha antes de as tags saírem.
        var texto = xml
            .Replace("</w:p>", "\n")
            .Replace("<w:br/>", "\n")
            .Replace("<w:tab/>", " ");

        texto = Regex.Replace(texto, "<[^>]+>", string.Empty);

        return texto
            .Replace("&amp;", "&")
            .Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&apos;", "'");
    }

    // ----------------------------------------------------------------- pdf

    /// <summary>Texto de um PDF com fonte embutida, como o Word exporta.</summary>
    /// <remarks>
    /// PDF não guarda texto: guarda instruções de desenho, e cada letra é um código
    /// interno da fonte. O mapa que traduz esses códigos de volta ao Unicode é o
    /// <c>ToUnicode</c>, e é ele que faz este método funcionar. PDF sem esse mapa —
    /// página escaneada, por exemplo — não tem texto nenhum para extrair, e aqui devolve
    /// nulo: quem chama trata como arquivo ilegível.
    /// </remarks>
    private static string? DoPdf(string caminho)
    {
        byte[] bytes;
        using (var fluxo = AbrirCompartilhado(caminho))
        {
            bytes = new byte[fluxo.Length];
            fluxo.ReadExactly(bytes);
        }

        // Latin-1 mapeia byte a caractere sem perder nada: serve para procurar os
        // marcadores do formato sem estragar o que ainda está comprimido.
        var latin = Encoding.Latin1;
        var cru = latin.GetString(bytes);

        var partes = new List<string>();
        foreach (var (inicio, tamanho) in LocalizarStreams(cru))
        {
            var inflado = Inflar(bytes, inicio, tamanho, latin);
            if (inflado is not null) partes.Add(inflado);
        }

        if (partes.Count == 0) return null;

        var mapa = MontarMapaDeFonte(partes);
        if (mapa.Count == 0) return null;

        var texto = new StringBuilder();
        foreach (var parte in partes)
        {
            // Cada BT..ET é uma caixa de texto desenhada na página.
            foreach (Match caixa in Regex.Matches(parte, @"BT(.*?)ET", RegexOptions.Singleline))
            {
                var linha = new StringBuilder();
                foreach (Match desenho in Regex.Matches(caixa.Groups[1].Value, @"<([0-9A-Fa-f]+)>\s*Tj"))
                {
                    linha.Append(Decodificar(desenho.Groups[1].Value, mapa));
                }

                if (linha.Length > 0) texto.Append(linha).Append(' ');
            }
        }

        var resultado = texto.ToString();

        // O exportador quebra cada palavra numa caixa própria, então a pontuação chega
        // separada do que vem antes: "CARDOSO ," precisa virar "CARDOSO,".
        resultado = Regex.Replace(resultado, @"\s+([,.;:])", "$1");
        return Regex.Replace(resultado, @"\s{2,}", " ").Trim();
    }

    /// <summary>Onde começa e quanto ocupa cada bloco entre stream e endstream.</summary>
    private static List<(int Inicio, int Tamanho)> LocalizarStreams(string cru)
    {
        var achados = new List<(int, int)>();
        var busca = 0;

        while (true)
        {
            var marca = cru.IndexOf("stream", busca, StringComparison.Ordinal);
            if (marca < 0) break;

            // "endstream" também termina em "stream": não é começo de bloco.
            if (marca >= 3 && cru.AsSpan(marca - 3, 3) is "end")
            {
                busca = marca + 6;
                continue;
            }

            var dados = marca + 6;
            while (dados < cru.Length && (cru[dados] == '\r' || cru[dados] == '\n')) dados++;

            var fim = cru.IndexOf("endstream", dados, StringComparison.Ordinal);
            if (fim < 0) break;

            achados.Add((dados, fim - dados));
            busca = fim + 9;
        }

        return achados;
    }

    /// <summary>Descomprime um bloco. Nulo quando não era Flate.</summary>
    private static string? Inflar(byte[] bytes, int inicio, int tamanho, Encoding latin)
    {
        try
        {
            // Os dois primeiros bytes são cabeçalho zlib; o descompressor quer o resto.
            using var origem = new MemoryStream(bytes, inicio + 2, tamanho - 2);
            using var deflate = new DeflateStream(origem, CompressionMode.Decompress);
            using var destino = new MemoryStream();

            deflate.CopyTo(destino);
            return latin.GetString(destino.ToArray());
        }
        catch (Exception)
        {
            // Imagem, fonte binária ou bloco não comprimido: não é texto.
            return null;
        }
    }

    /// <summary>O mapa código-da-fonte → Unicode, juntando todos os ToUnicode do arquivo.</summary>
    private static Dictionary<string, char> MontarMapaDeFonte(List<string> partes)
    {
        var mapa = new Dictionary<string, char>();

        foreach (var parte in partes)
        {
            // bfchar: um código de cada vez.
            foreach (Match bloco in Regex.Matches(parte, @"beginbfchar(.*?)endbfchar", RegexOptions.Singleline))
            {
                foreach (Match par in Regex.Matches(bloco.Groups[1].Value, @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]{4,})>"))
                {
                    var destino = par.Groups[2].Value;
                    mapa[par.Groups[1].Value.ToUpperInvariant()] =
                        (char)Convert.ToInt32(destino[..4], 16);
                }
            }

            // bfrange: uma faixa de códigos seguidos.
            foreach (Match bloco in Regex.Matches(parte, @"beginbfrange(.*?)endbfrange", RegexOptions.Singleline))
            {
                var padrao = @"<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]+)>\s*<([0-9A-Fa-f]{4,})>";
                foreach (Match faixa in Regex.Matches(bloco.Groups[1].Value, padrao))
                {
                    var de = Convert.ToInt32(faixa.Groups[1].Value, 16);
                    var ate = Convert.ToInt32(faixa.Groups[2].Value, 16);
                    var unicode = Convert.ToInt32(faixa.Groups[3].Value[..4], 16);

                    // Faixa aberta demais é sinal de arquivo estranho; não vale o risco.
                    if (ate < de || ate - de > 5000) continue;

                    for (var codigo = de; codigo <= ate; codigo++)
                    {
                        mapa[codigo.ToString("X4")] = (char)(unicode + codigo - de);
                    }
                }
            }
        }

        return mapa;
    }

    /// <summary>Converte uma sequência de códigos de 4 dígitos no texto que ela representa.</summary>
    private static string Decodificar(string hex, Dictionary<string, char> mapa)
    {
        var texto = new StringBuilder();

        for (var i = 0; i + 4 <= hex.Length; i += 4)
        {
            if (mapa.TryGetValue(hex.Substring(i, 4).ToUpperInvariant(), out var letra))
            {
                texto.Append(letra);
            }
        }

        return texto.ToString();
    }
}
