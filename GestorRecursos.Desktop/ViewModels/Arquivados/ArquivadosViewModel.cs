using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Uma aba da área de arquivados.</summary>
public partial class AbaArquivoViewModel(string titulo, TipoArquivado tipo, bool pronta) : ObservableObject
{
    public string Titulo { get; } = titulo;
    public TipoArquivado Tipo { get; } = tipo;

    /// <summary>Aba cujo tipo ainda não tem tela. Fica à mostra, mas desligada.</summary>
    public bool Pronta { get; } = pronta;

    [ObservableProperty]
    public partial bool Escolhida { get; set; }
}

/// <summary>O que pode estar arquivado.</summary>
public enum TipoArquivado
{
    Clientes,
    Veiculos,
    Processos
}

/// <summary>A área de arquivados: o que foi tirado das listas, por tipo.</summary>
/// <remarks>
/// Separar o arquivo do dia a dia resolve uma confusão real: na listagem comum, um cliente
/// arquivado encontrado pela busca aparecia misturado aos ativos. Aqui também é o único
/// lugar onde se apaga de verdade — a regra desta fase é que ninguém apaga sem antes arquivar.
/// </remarks>
public partial class ArquivadosViewModel : ViewModelBase
{
    public ObservableCollection<AbaArquivoViewModel> Abas { get; } =
    [
        new("Clientes", TipoArquivado.Clientes, pronta: true),
        new("Veículos", TipoArquivado.Veiculos, pronta: false),
        new("Processos", TipoArquivado.Processos, pronta: false)
    ];

    [ObservableProperty]
    public partial ViewModelBase? Conteudo { get; set; }

    public ArquivadosViewModel()
    {
        Abrir(TipoArquivado.Clientes);
    }

    [RelayCommand]
    private void Abrir(TipoArquivado tipo)
    {
        foreach (var aba in Abas)
        {
            aba.Escolhida = aba.Tipo == tipo;
        }

        Conteudo = tipo switch
        {
            TipoArquivado.Clientes => new ListaClientesViewModel(modoArquivo: true),
            _ => null
        };
    }
}
