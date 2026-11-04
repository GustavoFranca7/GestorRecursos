using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace GestorRecursos.Desktop.Servicos;

/// <summary>Salvar um arquivo e abri-lo, de dentro de um ViewModel.</summary>
/// <remarks>
/// Mesmo arranjo da <see cref="AreaDeTransferencia"/>: a caixa de "salvar como" pertence à
/// janela, e o ViewModel não conhece janela. Ela se registra ao abrir.
/// </remarks>
public static class Arquivos
{
    private static TopLevel? _janela;

    /// <summary>Chamado uma vez, quando a janela principal abre.</summary>
    public static void Registrar(TopLevel janela) => _janela = janela;

    /// <summary>Pergunta onde gravar. Devolve nulo quando desiste.</summary>
    public static async Task<string?> OndeSalvarPdfAsync(string nomeSugerido)
    {
        if (_janela?.StorageProvider is not { } armazenamento)
        {
            return null;
        }

        var escolhido = await armazenamento.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar documento",
            SuggestedFileName = nomeSugerido,
            DefaultExtension = "pdf",
            FileTypeChoices = new List<FilePickerFileType>
            {
                new("Documento PDF") { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] }
            }
        });

        return escolhido?.TryGetLocalPath();
    }

    /// <summary>Pergunta onde gravar um anexo, pelo tipo que o nome dele indica.</summary>
    /// <remarks>
    /// Separado do <see cref="OndeSalvarPdfAsync"/> porque o anexo pode ser Word, e
    /// oferecer ".pdf" para um .docx faria a pessoa gravar um arquivo que não abre.
    /// </remarks>
    public static async Task<string?> OndeSalvarAnexoAsync(string nomeDoArquivo)
    {
        if (_janela?.StorageProvider is not { } armazenamento)
        {
            return null;
        }

        var extensao = Path.GetExtension(nomeDoArquivo).TrimStart('.').ToLowerInvariant();
        var ehWord = extensao == "docx";

        var escolhido = await armazenamento.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Salvar documento",
            SuggestedFileName = NomeSeguro(nomeDoArquivo),
            DefaultExtension = string.IsNullOrEmpty(extensao) ? "pdf" : extensao,
            FileTypeChoices = ehWord
                ?
                [
                    new("Documento do Word")
                    {
                        Patterns = ["*.docx"],
                        MimeTypes =
                        [
                            "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                        ]
                    }
                ]
                :
                [
                    new("Documento PDF") { Patterns = ["*.pdf"], MimeTypes = ["application/pdf"] }
                ]
        });

        return escolhido?.TryGetLocalPath();
    }

    /// <summary>Um documento escolhido para anexar: o nome que ele tinha e o conteúdo.</summary>
    public sealed record DocumentoEscolhido(string Nome, byte[] Conteudo);

    /// <summary>Pergunta qual documento anexar. Devolve nulo quando desiste.</summary>
    /// <remarks>
    /// O filtro da janela é conveniência, não regra: ele guia a escolha, mas qualquer
    /// um troca o filtro para "todos os arquivos". Quem decide o que entra é a
    /// conferência do conteúdo, em <c>DocumentoAnexo</c>.
    /// </remarks>
    public static async Task<DocumentoEscolhido?> EscolherDocumentoAsync()
    {
        if (_janela?.StorageProvider is not { } armazenamento)
        {
            return null;
        }

        var escolhidos = await armazenamento.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Escolher documento",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new("Documentos (PDF e Word)")
                {
                    Patterns = ["*.pdf", "*.docx"],
                    MimeTypes =
                    [
                        "application/pdf",
                        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                    ]
                }
            ]
        });

        if (escolhidos.Count == 0)
        {
            return null;
        }

        var escolhido = escolhidos[0];

        await using var fonte = await escolhido.OpenReadAsync();
        using var memoria = new MemoryStream();
        await fonte.CopyToAsync(memoria);

        return new DocumentoEscolhido(escolhido.Name, memoria.ToArray());
    }

    /// <summary>Grava o conteúdo numa pasta temporária e abre no programa do sistema.</summary>
    /// <remarks>
    /// O documento mora no banco; para ser visto, precisa existir como arquivo em algum
    /// lugar. A pasta temporária é esse lugar: o Windows a limpa, e nada do cliente fica
    /// espalhado pelo disco sem controle.
    ///
    /// Cada anexo tem a sua subpasta, pelo id, para dois documentos de mesmo nome não se
    /// sobrescreverem — e para abrir o mesmo anexo duas vezes não gerar "arquivo em uso".
    /// </remarks>
    public static async Task AbrirConteudoAsync(int id, string nome, byte[] conteudo)
    {
        var pasta = Path.Combine(Path.GetTempPath(), "GestorRecursos", "anexos", id.ToString());
        Directory.CreateDirectory(pasta);

        var caminho = Path.Combine(pasta, NomeSeguro(nome));
        await File.WriteAllBytesAsync(caminho, conteudo);

        Abrir(caminho);
    }

    /// <summary>Tira do nome o que o sistema de arquivos não aceita.</summary>
    private static string NomeSeguro(string nome)
    {
        var limpo = new string(nome
            .Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c)
            .ToArray());

        return string.IsNullOrWhiteSpace(limpo) ? "documento" : limpo;
    }

    /// <summary>Pergunta qual pasta ler. Devolve nulo quando desiste.</summary>
    public static async Task<string?> EscolherPastaAsync(string titulo)
    {
        if (_janela?.StorageProvider is not { } armazenamento)
        {
            return null;
        }

        var escolhidas = await armazenamento.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = titulo,
            AllowMultiple = false
        });

        return escolhidas.Count > 0 ? escolhidas[0].TryGetLocalPath() : null;
    }

    /// <summary>Abre o arquivo no programa que o sistema usa para o tipo.</summary>
    /// <remarks>
    /// Falha em silêncio de propósito: o arquivo já foi gravado, e não abrir o leitor de
    /// PDF não é motivo para a tela acusar erro sobre um documento que existe.
    /// </remarks>
    public static void Abrir(string caminho)
    {
        try
        {
            Process.Start(new ProcessStartInfo(caminho) { UseShellExecute = true });
        }
        catch (Exception)
        {
            // Sem leitor associado, ou caminho bloqueado: o documento continua no disco.
        }
    }
}
