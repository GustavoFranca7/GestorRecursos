using System;
using System.Collections.Generic;
using System.Diagnostics;
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
