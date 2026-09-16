using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GerenciadorClientes.Core.Models;

namespace GerenciadorClientes.Desktop.ViewModels;

// Uma linha da lista: o cliente mais o estado da setinha (endereco aberto ou fechado).
public partial class ClienteItemViewModel : ViewModelBase
{
    public ClienteItemViewModel(Cliente cliente)
    {
        Cliente = cliente;
    }

    public Cliente Cliente { get; }

    [ObservableProperty]
    public partial bool Expandido { get; set; }

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
