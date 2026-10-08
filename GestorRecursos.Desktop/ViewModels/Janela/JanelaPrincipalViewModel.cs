using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>O esqueleto da janela: menu lateral, busca global e a área de conteúdo.</summary>
/// <remarks>
/// Só cuida de navegação e do estado do menu. O conteúdo de cada seção é
/// responsabilidade do ViewModel próprio dela, que entra em <see cref="Conteudo"/>.
/// </remarks>
public partial class JanelaPrincipalViewModel : ViewModelBase
{
    public ObservableCollection<ItemMenuViewModel> ItensPrincipais { get; } =
    [
        new("Dashboard",  "IconeMenuDashboard",  SecaoApp.Dashboard),
        new("Clientes",   "IconeMenuClientes",   SecaoApp.Clientes),
        new("Processos",  "IconeMenuProcessos",  SecaoApp.Processos),
        new("Financeiro", "IconeMenuFinanceiro", SecaoApp.Financeiro),
        new("Agenda",     "IconeMenuAgenda",     SecaoApp.Agenda)
    ];

    public ObservableCollection<ItemMenuViewModel> ItensCadastro { get; } =
    [
        new("Veículos",          "IconeMenuVeiculos",      SecaoApp.Veiculos),
        new("Serviços e preços", "IconeMenuServicos",      SecaoApp.Servicos),
        new("Arquivados",        "IconeExcluir",           SecaoApp.Arquivados),
        new("Configurações",     "IconeMenuConfiguracoes", SecaoApp.Configuracoes)
    ];

    private IEnumerable<ItemMenuViewModel> TodosOsItens => ItensPrincipais.Concat(ItensCadastro);

    /// <summary>Menu recolhido mostra só os ícones.</summary>
    [ObservableProperty]
    public partial bool MenuRetraido { get; set; }

    [ObservableProperty]
    public partial SecaoApp SecaoAtual { get; set; } = SecaoApp.Clientes;

    [ObservableProperty]
    public partial string TituloSecao { get; set; } = "Clientes";

    /// <summary>ViewModel da tela que está aberta. Nulo enquanto a seção não existe.</summary>
    [ObservableProperty]
    public partial ViewModelBase? Conteudo { get; set; }

    /// <summary>Aviso de "copiado", um só para o sistema inteiro.</summary>
    [ObservableProperty]
    public partial bool MostrandoCopiado { get; set; }

    [ObservableProperty]
    public partial string TextoCopiado { get; set; } = string.Empty;

    private int _ultimaCopia;

    public JanelaPrincipalViewModel()
    {
        AreaDeTransferencia.Copiou += AvisarCopia;
        Navegar(SecaoApp.Clientes);
    }

    /// <summary>Mostra o que foi copiado por alguns segundos, venha de qual tela vier.</summary>
    private async void AvisarCopia(string texto)
    {
        TextoCopiado = texto;
        MostrandoCopiado = true;

        var marca = ++_ultimaCopia;
        await Task.Delay(1800);

        // Outra cópia no meio do caminho manda no aviso: esta já perdeu a vez.
        if (marca == _ultimaCopia)
        {
            MostrandoCopiado = false;
        }
    }

    [RelayCommand]
    private void AlternarMenu() => MenuRetraido = !MenuRetraido;

    [RelayCommand]
    private void Navegar(SecaoApp secao)
    {
        SecaoAtual = secao;

        foreach (var item in TodosOsItens)
        {
            item.Ativo = item.Secao == secao;
        }

        TituloSecao = TodosOsItens.FirstOrDefault(i => i.Secao == secao)?.Titulo ?? string.Empty;

        // Cada seção recebe seu ViewModel aqui conforme as telas ficam prontas.
        // Lista nova a cada visita: voltar para Clientes traz o que está no banco agora.
        Conteudo = secao switch
        {
            SecaoApp.Clientes => new ClientesViewModel(),
            SecaoApp.Arquivados => new ArquivadosViewModel(),
            _ => null
        };
    }
}
