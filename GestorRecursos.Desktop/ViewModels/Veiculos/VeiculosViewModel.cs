using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>A seção Veículos: o carro como coisa própria, e não como anexo de um cliente.</summary>
/// <remarks>
/// Responde o RF-13 — "achar o cliente a partir da placa" —, que é a pergunta que abriu o
/// projeto: <i>"às vezes volta a placa, mas eu não sei quem é o dono da placa"</i>.
///
/// A busca global já acha o cliente pela placa, mas só com a placa inteira e devolvendo
/// uma linha no meio de outros resultados. O que não existia era ver o <b>carro</b>: um
/// veículo tem um dono e pode ter vários condutores indicados, e acumula multas de épocas
/// diferentes, de clientes diferentes. A ficha de cada cliente mostra só o pedaço dele.
///
/// E há o que só uma lista mostra, por ser varredura e não consulta: o carro que ficou
/// <b>sem vínculo</b> e o que está <b>sem os dados que o recurso usa</b>. Nenhum dos dois
/// aparece quando se procura por placa, porque ninguém procura o que não sabe que existe.
/// </remarks>
public partial class VeiculosViewModel : ViewModelBase
{
    private readonly VeiculoDAO _veiculos = new();
    private readonly Action<int>? _aoAbrirCliente;

    private List<VeiculoViewModel> _todos = [];

    public ObservableCollection<VeiculoViewModel> Veiculos { get; } = [];

    public VeiculosViewModel(Action<int>? aoAbrirCliente = null)
    {
        _aoAbrirCliente = aoAbrirCliente;

        // No preview do Rider não há banco nem wallet: a tela abriria com erro de conexão.
        if (!Design.IsDesignMode)
        {
            CarregarCommand.Execute(null);
        }
    }

    // ---------------------------------------------------------------------
    // Filtros
    // ---------------------------------------------------------------------

    public const string TodosOsVeiculos = "Todos os veículos";
    public const string SemVinculo = "Sem dono vinculado";
    public const string FaltaDado = "Faltam dados do recurso";
    public const string ComRecurso = "Com recurso em andamento";

    public ObservableCollection<string> Filtros { get; } =
    [
        TodosOsVeiculos, SemVinculo, FaltaDado, ComRecurso
    ];

    [ObservableProperty]
    public partial string FiltroEscolhido { get; set; } = TodosOsVeiculos;

    [ObservableProperty]
    public partial string TermoBusca { get; set; } = string.Empty;

    public bool TemBusca => TermoBusca.Length > 0;

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? Erro { get; set; }

    /// <summary>O formulário de veículo, por cima da lista. Nulo quando não há.</summary>
    [ObservableProperty]
    public partial ViewModelBase? Formulario { get; set; }

    [ObservableProperty]
    public partial string Resumo { get; set; } = string.Empty;

    /// <summary>Quantos carros estão sem ninguém ligado a eles, no total.</summary>
    [ObservableProperty]
    public partial int QuantosSemVinculo { get; set; }

    [ObservableProperty]
    public partial int QuantosFaltandoDado { get; set; }

    public bool TemSemVinculo => QuantosSemVinculo > 0;
    public bool TemFaltandoDado => QuantosFaltandoDado > 0;

    public bool SemNada => !Carregando && Veiculos.Count == 0;

    public string MensagemVazia => TemBusca || FiltroEscolhido != TodosOsVeiculos
        ? "Nenhum veículo para o que foi escolhido."
        : "Nenhum veículo cadastrado ainda.";

    // ---------------------------------------------------------------------
    // Carga
    // ---------------------------------------------------------------------

    [RelayCommand]
    private async Task Carregar()
    {
        Carregando = true;
        Erro = null;

        try
        {
            var lista = await _veiculos.ListarAsync();

            _todos = [.. lista.Select(v => new VeiculoViewModel(v))];

            QuantosSemVinculo = lista.Count(v => v.SemVinculo);
            QuantosFaltandoDado = lista.Count(v => v.FaltaDadoDoRecurso);

            var quantos = lista.Count;
            Resumo = quantos == 1 ? "1 veículo" : $"{quantos} veículos";

            Filtrar();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível carregar os veículos: {excecao.Message}";
        }
        finally
        {
            Carregando = false;
            OnPropertyChanged(nameof(SemNada));
            OnPropertyChanged(nameof(TemSemVinculo));
            OnPropertyChanged(nameof(TemFaltandoDado));
        }
    }

    private void Filtrar()
    {
        var itens = _todos.AsEnumerable();

        itens = FiltroEscolhido switch
        {
            SemVinculo => itens.Where(v => v.SemVinculo),
            FaltaDado => itens.Where(v => v.FaltaDado),
            ComRecurso => itens.Where(v => v.TemRecursos),
            _ => itens
        };

        var termo = TermoBusca.Trim().ToLowerInvariant();

        if (termo.Length > 0)
        {
            itens = itens.Where(v => v.Busca.Contains(termo, StringComparison.Ordinal));
        }

        Veiculos.Clear();
        foreach (var veiculo in itens)
        {
            Veiculos.Add(veiculo);
        }

        OnPropertyChanged(nameof(SemNada));
        OnPropertyChanged(nameof(MensagemVazia));
    }

    partial void OnFiltroEscolhidoChanged(string value) => Filtrar();

    partial void OnTermoBuscaChanged(string value)
    {
        Filtrar();
        OnPropertyChanged(nameof(TemBusca));
    }

    [RelayCommand]
    private void LimparBusca() => TermoBusca = string.Empty;

    /// <summary>Atalho dos avisos do topo para o filtro correspondente.</summary>
    [RelayCommand]
    private void Filtrar(string? filtro)
    {
        if (!string.IsNullOrWhiteSpace(filtro))
        {
            FiltroEscolhido = filtro;
        }
    }

    // ---------------------------------------------------------------------
    // O histórico do carro
    // ---------------------------------------------------------------------

    /// <summary>Abre ou fecha a linha, buscando o histórico na primeira vez.</summary>
    [RelayCommand]
    private async Task Alternar(VeiculoViewModel? veiculo)
    {
        if (veiculo is null)
        {
            return;
        }

        if (veiculo.Aberto)
        {
            veiculo.Aberto = false;
            return;
        }

        // Uma linha aberta por vez: duas histórias abertas empurram a lista para longe
        // e tiram justamente a comparação que a lista serve para fazer.
        foreach (var outro in Veiculos.Where(v => v.Aberto))
        {
            outro.Aberto = false;
        }

        veiculo.Aberto = true;

        if (veiculo.JaCarregou)
        {
            return;
        }

        veiculo.CarregandoHistorico = true;

        try
        {
            veiculo.Preencher(await _veiculos.AbrirAsync(veiculo.Id));
        }
        catch (Exception excecao)
        {
            veiculo.Aberto = false;
            Erro = $"Não foi possível abrir o histórico do veículo: {excecao.Message}";
        }
        finally
        {
            veiculo.CarregandoHistorico = false;
        }
    }

    // ---------------------------------------------------------------------
    // Ações
    // ---------------------------------------------------------------------

    [RelayCommand]
    private void NovoVeiculo() =>
        Formulario = new CadastroDeVeiculoViewModel(
            aoCancelar: () => Formulario = null,
            aoSalvar: () =>
            {
                Formulario = null;
                CarregarCommand.Execute(null);
            });

    [RelayCommand]
    private void EditarVeiculo(VeiculoViewModel? veiculo)
    {
        if (veiculo is null)
        {
            return;
        }

        Formulario = new CadastroDeVeiculoViewModel(
            veiculo.Dados,
            aoCancelar: () => Formulario = null,
            aoSalvar: () =>
            {
                Formulario = null;
                CarregarCommand.Execute(null);
            });
    }

    /// <summary>Leva à ficha do cliente — o fim do caminho que começa na placa.</summary>
    /// <remarks>Aceita nulo porque a multa pode não ter recurso contratado.</remarks>
    [RelayCommand]
    private void AbrirCliente(int? clienteId)
    {
        if (clienteId is > 0)
        {
            _aoAbrirCliente?.Invoke(clienteId.Value);
        }
    }
}
