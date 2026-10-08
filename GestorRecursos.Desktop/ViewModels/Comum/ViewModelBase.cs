using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Base de todos os ViewModels.</summary>
/// <remarks>
/// Copiar mora aqui porque é a mesma ação em qualquer tela: nome, CPF, placa ou AIT, na
/// listagem ou na ficha, copiam do mesmo jeito e avisam do mesmo jeito. Repetir isso em
/// cada ViewModel seria o caminho para as telas divergirem.
/// </remarks>
public abstract partial class ViewModelBase : ObservableObject
{
    /// <summary>Texto que a tela mostra quando não há dado: não é dado, não se copia.</summary>
    public const string SemValor = "não informado";

    [RelayCommand]
    private Task Copiar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) || texto == SemValor
            ? Task.CompletedTask
            : AreaDeTransferencia.CopiarAsync(texto);
}
