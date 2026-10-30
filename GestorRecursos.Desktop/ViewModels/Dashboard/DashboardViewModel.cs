using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Que parte dos prazos abertos a lista mostra.</summary>
public enum FiltroPrazos
{
    /// <summary>Vencidos e os próximos sete dias: o que precisa de ação agora.</summary>
    Semana,

    /// <summary>Só os que já passaram da data.</summary>
    Vencidos,

    Todos
}

/// <summary>A visão do dia: o que vence e quem está devendo (RF-38, RF-48, RNF-13).</summary>
/// <remarks>
/// É a primeira tela do sistema porque responde à pergunta da segunda de manhã. Antes
/// dela, saber o que vencia na semana exigia abrir cliente por cliente — e com 60 a 70
/// recursos por mês isso não se faz, então a planilha continuava viva ao lado do sistema.
///
/// Duas listas, e não gráficos: a RNF-13 pede formato de planilha, que é o que ela já lê.
/// </remarks>
public partial class DashboardViewModel : ViewModelBase
{
    private readonly PainelDAO _painel = new();
    private readonly Action<int>? _aoAbrirCliente;

    /// <summary>Todos os prazos abertos. As abas filtram esta lista sem voltar ao banco.</summary>
    private IReadOnlyList<PrazoDoDia> _todosOsPrazos = [];

    public ObservableCollection<LinhaPrazoViewModel> Prazos { get; } = [];
    public ObservableCollection<LinhaCobrancaViewModel> Cobrancas { get; } = [];

    public DashboardViewModel(Action<int>? aoAbrirCliente = null)
    {
        _aoAbrirCliente = aoAbrirCliente;

        // No preview do Rider não há banco nem wallet: a tela abriria com erro de conexão.
        if (!Design.IsDesignMode)
        {
            CarregarCommand.Execute(null);
        }
    }

    [ObservableProperty]
    public partial FiltroPrazos Filtro { get; set; } = FiltroPrazos.Semana;

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? Erro { get; set; }

    [ObservableProperty]
    public partial int Vencidos { get; set; }

    [ObservableProperty]
    public partial int Hoje { get; set; }

    [ObservableProperty]
    public partial int NaSemana { get; set; }

    [ObservableProperty]
    public partial int QuantasCobrancas { get; set; }

    [ObservableProperty]
    public partial string TotalEmAberto { get; set; } = Formato.Dinheiro(0);

    /// <summary>"quinta-feira, 30 de outubro de 2026", no alto da tela.</summary>
    public string DataDeHoje => Formato.DiaPorExtenso(DateTime.Today);

    public bool FiltroSemana => Filtro == FiltroPrazos.Semana;
    public bool FiltroVencidos => Filtro == FiltroPrazos.Vencidos;
    public bool FiltroTodos => Filtro == FiltroPrazos.Todos;

    public bool SemPrazos => !Carregando && Erro is null && Prazos.Count == 0;
    public bool SemCobrancas => !Carregando && Erro is null && Cobrancas.Count == 0;

    /// <summary>O que a lista de prazos diz quando está vazia, conforme a aba.</summary>
    public string MensagemSemPrazos => Filtro switch
    {
        FiltroPrazos.Semana => "Nenhum prazo vencendo nos próximos sete dias.",
        FiltroPrazos.Vencidos => "Nenhum prazo vencido. Está tudo em dia.",
        _ => "Nenhum prazo de protocolo em aberto."
    };

    [RelayCommand]
    private async Task Carregar()
    {
        Carregando = true;
        Erro = null;

        try
        {
            var visao = await _painel.MontarAsync();

            _todosOsPrazos = visao.Prazos;
            Vencidos = visao.Vencidos;
            Hoje = visao.Hoje;
            NaSemana = visao.NaSemana;

            QuantasCobrancas = visao.Cobrancas.Count;
            TotalEmAberto = Formato.Dinheiro(visao.TotalEmAberto);

            Cobrancas.Clear();
            foreach (var cobranca in visao.Cobrancas)
            {
                Cobrancas.Add(new LinhaCobrancaViewModel(cobranca));
            }

            AplicarFiltro();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível carregar o painel: {excecao.Message}";
            _todosOsPrazos = [];
            Prazos.Clear();
            Cobrancas.Clear();
            Vencidos = Hoje = NaSemana = QuantasCobrancas = 0;
            TotalEmAberto = Formato.Dinheiro(0);
        }
        finally
        {
            Carregando = false;
            AvisarEstadoDasListas();
        }
    }

    [RelayCommand]
    private void TrocarFiltro(FiltroPrazos filtro) => Filtro = filtro;

    partial void OnFiltroChanged(FiltroPrazos value)
    {
        AplicarFiltro();
        OnPropertyChanged(nameof(FiltroSemana));
        OnPropertyChanged(nameof(FiltroVencidos));
        OnPropertyChanged(nameof(FiltroTodos));
        OnPropertyChanged(nameof(MensagemSemPrazos));
        AvisarEstadoDasListas();
    }

    /// <summary>Leva à ficha do cliente daquela linha, sem perder o painel de vista.</summary>
    [RelayCommand]
    private void AbrirCliente(int clienteId) => _aoAbrirCliente?.Invoke(clienteId);

    private void AplicarFiltro()
    {
        var filtrados = Filtro switch
        {
            FiltroPrazos.Semana => _todosOsPrazos.Where(p => p.Dias <= 7),
            FiltroPrazos.Vencidos => _todosOsPrazos.Where(p => p.Dias < 0),
            _ => _todosOsPrazos
        };

        Prazos.Clear();
        foreach (var prazo in filtrados)
        {
            Prazos.Add(new LinhaPrazoViewModel(prazo));
        }
    }

    private void AvisarEstadoDasListas()
    {
        OnPropertyChanged(nameof(SemPrazos));
        OnPropertyChanged(nameof(SemCobrancas));
    }
}
