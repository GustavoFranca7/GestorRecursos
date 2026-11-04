using CommunityToolkit.Mvvm.ComponentModel;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Um documento anexado, na lista da aba Documentos.</summary>
/// <remarks>
/// Só o que a linha mostra. O documento em si fica no banco e só é lido quando alguém
/// manda abrir — uma lista de dez anexos não pode custar dez megabytes de rede.
/// </remarks>
public partial class AnexoDaFichaViewModel(Arquivo anexo) : ObservableObject
{
    public int Id { get; } = anexo.Id;
    public string Nome { get; } = anexo.NomeArquivo;
    public string Tipo { get; } = Rotulos.TipoDeAnexo(anexo.Tipo);
    public string Tamanho { get; } = anexo.TamanhoLegivel;
    public string Quando { get; } = Formato.Data(anexo.DataUpload);

    /// <summary>Verdadeiro enquanto o documento está sendo buscado para abrir.</summary>
    /// <remarks>
    /// Um PDF de 8 MB demora o bastante para alguém clicar duas vezes achando que não
    /// funcionou. O botão se desliga enquanto busca.
    /// </remarks>
    [ObservableProperty]
    public partial bool Abrindo { get; set; }
}
