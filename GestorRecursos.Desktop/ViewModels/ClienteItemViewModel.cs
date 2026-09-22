using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

// Uma linha da lista: o cliente, o estado da setinha e a caixinha de selecao.
public partial class ClienteItemViewModel : ViewModelBase
{
    // Avisa a tela principal quando a caixinha muda (para contar os selecionados).
    private readonly Action? _aoMudarSelecao;

    public ClienteItemViewModel(Cliente cliente, Action? aoMudarSelecao = null)
    {
        Cliente = cliente;
        _aoMudarSelecao = aoMudarSelecao;
    }

    public Cliente Cliente { get; }

    [ObservableProperty]
    public partial bool Expandido { get; set; }

    // Caixinha da direita, marcada para exclusao em grupo.
    [ObservableProperty]
    public partial bool Selecionado { get; set; }

    partial void OnSelecionadoChanged(bool value) => _aoMudarSelecao?.Invoke();

    // Setinha: ">" fechado, "v" aberto.
    public string Seta => Expandido ? "v" : ">";

    public bool TemEndereco => Cliente.Endereco is not null;

    // Etiqueta da coluna CIDADE, ex.: "JOINVILLE SC".
    public string CidadeUf => Cliente.Endereco is null
        ? string.Empty
        : $"{Cliente.Endereco.Cidade} - {Cliente.Endereco.Estado}".ToUpperInvariant();

    partial void OnExpandidoChanged(bool value) => OnPropertyChanged(nameof(Seta));

    [RelayCommand]
    private void Alternar()
    {
        Expandido = !Expandido;
    }
}
