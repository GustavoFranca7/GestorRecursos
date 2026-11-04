using System.IO.Compression;

namespace GestorRecursos.Core.Validacao;

/// <summary>O que a conferência de um anexo concluiu.</summary>
/// <remarks>
/// <c>Problema</c> é o texto que vai para a tela. Vazio quando passou.
/// </remarks>
public sealed record ConferenciaDeAnexo(bool Aceito, string Problema)
{
    public static readonly ConferenciaDeAnexo Ok = new(true, string.Empty);

    public static ConferenciaDeAnexo Recusado(string motivo) => new(false, motivo);
}

// Camada Core / Validacao: o que pode entrar como anexo do cliente.
//
// Vale para o Desktop e para a API: as duas gravam na mesma tabela, e a regra não pode
// existir só na tela.
public static class DocumentoAnexo
{
    /// <summary>O teto por arquivo. Espelha o CK_ARQUIVOS_TAMANHO.</summary>
    /// <remarks>
    /// 10 MB cobre com folga um documento digitalizado em preto e branco a 300 dpi. A
    /// instância tem 20 GB para texto e documentos juntos; sem teto, uma digitalização
    /// colorida a 600 dpi de um processo inteiro consome o espaço de um ano numa tarde.
    /// </remarks>
    public const int TamanhoMaximo = 10 * 1024 * 1024;

    /// <summary>As extensões que a janela de escolha oferece.</summary>
    public static readonly string[] ExtensoesAceitas = [".pdf", ".docx"];

    // O que o arquivo e, lido dos primeiros bytes. Extensao nao serve: renomear
    // ferias.mp4 para procuracao.pdf e um clique, e o banco engoliria 300 MB.
    private static readonly byte[] AssinaturaPdf = [0x25, 0x50, 0x44, 0x46, 0x2D]; // %PDF-
    private static readonly byte[] AssinaturaZip = [0x50, 0x4B, 0x03, 0x04];       // PK..

    /// <summary>Confere tamanho e conteúdo antes de qualquer coisa ir para o banco.</summary>
    public static ConferenciaDeAnexo Conferir(byte[] conteudo, string nomeArquivo)
    {
        if (conteudo.Length == 0)
        {
            return ConferenciaDeAnexo.Recusado(
                "O arquivo está vazio. Verifique se ele terminou de ser copiado ou digitalizado.");
        }

        if (conteudo.Length > TamanhoMaximo)
        {
            return ConferenciaDeAnexo.Recusado(
                $"O arquivo tem {Legivel(conteudo.Length)} e o limite é "
                + $"{Legivel(TamanhoMaximo)}. Digitalize em preto e branco, a 300 dpi, "
                + "ou divida o documento em partes.");
        }

        if (EhPdf(conteudo))
        {
            return ConferenciaDeAnexo.Ok;
        }

        if (EhDocx(conteudo))
        {
            return ConferenciaDeAnexo.Ok;
        }

        // A mensagem fala do conteúdo, não da extensão, porque é o conteúdo que foi
        // recusado: um .pdf que na verdade é uma foto cai aqui, e dizer "extensão
        // inválida" mandaria a pessoa conferir a única coisa que estava certa.
        return ConferenciaDeAnexo.Recusado(
            $"\"{nomeArquivo}\" não é um PDF nem um documento do Word. "
            + "São aceitos apenas estes dois formatos. Se for uma foto, use um "
            + "aplicativo de digitalização para gerar um PDF.");
    }

    /// <summary>Se o conteúdo é um PDF de verdade.</summary>
    private static bool EhPdf(byte[] conteudo) => Comeca(conteudo, AssinaturaPdf);

    /// <summary>Se o conteúdo é um .docx de verdade, e não outro arquivo compactado.</summary>
    /// <remarks>
    /// Um .docx é um zip, e todo zip começa igual — a assinatura sozinha aceitaria
    /// qualquer .zip renomeado. O que distingue é o que tem dentro: um documento do
    /// Word sempre traz "word/document.xml".
    /// </remarks>
    private static bool EhDocx(byte[] conteudo)
    {
        if (!Comeca(conteudo, AssinaturaZip))
        {
            return false;
        }

        try
        {
            using var memoria = new MemoryStream(conteudo, writable: false);
            using var pacote = new ZipArchive(memoria, ZipArchiveMode.Read);

            return pacote.Entries.Any(e =>
                e.FullName.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidDataException)
        {
            // Zip truncado ou corrompido: não é um .docx que se possa abrir depois.
            return false;
        }
    }

    private static bool Comeca(byte[] conteudo, byte[] assinatura) =>
        conteudo.Length >= assinatura.Length
        && conteudo.AsSpan(0, assinatura.Length).SequenceEqual(assinatura);

    private static string Legivel(int bytes) => bytes < 1024 * 1024
        ? $"{bytes / 1024.0:N0} KB"
        : $"{bytes / 1024.0 / 1024.0:N1} MB";
}
