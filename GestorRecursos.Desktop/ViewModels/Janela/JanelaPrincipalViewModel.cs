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
    public partial SecaoApp SecaoAtual { get; set; } = SecaoApp.Dashboard;

    [ObservableProperty]
    public partial string TituloSecao { get; set; } = "Dashboard";

    /// <summary>ViewModel da tela que está aberta. Nulo enquanto a seção não existe.</summary>
    [ObservableProperty]
    public partial ViewModelBase? Conteudo { get; set; }

    /// <summary>Os avisos flutuantes do canto, um só lugar para o sistema inteiro.</summary>
    /// <remarks>
    /// Coleção, e não um aviso só: duas ações seguidas (copiar e logo salvar) empilham
    /// em vez de uma atropelar a outra antes de ser lida.
    /// </remarks>
    public ObservableCollection<NotificacaoViewModel> Notificacoes { get; } = [];

    private readonly System.Action? _aoSair;

    public JanelaPrincipalViewModel(System.Action? aoSair = null)
    {
        _aoSair = aoSair;
        Servicos.Notificacoes.Chegou += Avisar;

        // Abre na visão do dia: é a pergunta da segunda de manhã — o que vence e quem deve.
        Navegar(SecaoApp.Dashboard);

        // Entrou com a senha que o administrador criou: antes de usar o sistema, troca.
        if (Sessao.Usuario is { PrimeiroAcesso: true } quem)
        {
            PrimeiroAcesso = new PrimeiroAcessoViewModel(quem, () => PrimeiroAcesso = null);
        }
    }

    /// <summary>A troca obrigatória da senha, por cima de tudo. Nula quando já foi feita.</summary>
    [ObservableProperty]
    public partial PrimeiroAcessoViewModel? PrimeiroAcesso { get; set; }

    /// <summary>Nome do ambiente, quando há um a anunciar. Nulo esconde o selo.</summary>
    public string? Ambiente => GestorRecursos.Core.Configuracoes.Ambiente;
    public bool ForaDeProducao => Ambiente is not null;

    /// <summary>Vermelho avisa; âmbar apenas informa.</summary>
    public string FundoDoAmbiente => GestorRecursos.Core.Configuracoes.AmbienteDelicado ? "AlertaClaro" : "AvisoClaro";
    public string CorDoAmbiente => GestorRecursos.Core.Configuracoes.AmbienteDelicado ? "Alerta" : "Aviso";

    /// <summary>Quem está no sistema, como aparece na barra de cima.</summary>
    public string UsuarioInicial => Sessao.Inicial;
    public string UsuarioNome => Sessao.Nome;
    public string UsuarioLogin => Sessao.Usuario is { } quem ? $"@{quem.Login}" : string.Empty;

    /// <summary>Volta para a tela de entrada.</summary>
    /// <remarks>
    /// A sessão não sobrevive à saída: entrar de novo pede a senha. Num sistema com dado
    /// de cliente, "sair" que não esquece quem era não é sair.
    /// </remarks>
    [RelayCommand]
    private void Sair()
    {
        Servicos.Notificacoes.Chegou -= Avisar;
        Sessao.Sair();
        _aoSair?.Invoke();
    }

    /// <summary>Põe o aviso no canto e tira quando ele se apaga, venha de qual tela vier.</summary>
    private async void Avisar(string texto, TomDaNotificacao tom)
    {
        var notificacao = new NotificacaoViewModel(texto, tom);
        Notificacoes.Add(notificacao);

        await notificacao.ViverAsync();

        Notificacoes.Remove(notificacao);
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
            SecaoApp.Dashboard => new DashboardViewModel(aoAbrirCliente: AbrirFichaDoCliente),
            SecaoApp.Clientes => new ClientesViewModel(),
            SecaoApp.Processos => new ProcessosViewModel(aoAbrirCliente: AbrirFichaDoCliente),
            SecaoApp.Financeiro => new FinanceiroViewModel(aoAbrirCliente: AbrirFichaDoCliente),
            SecaoApp.Arquivados => new ArquivadosViewModel(),
            SecaoApp.Servicos => new ServicosViewModel(),
            SecaoApp.Configuracoes => new ConfiguracoesViewModel(),
            _ => null
        };
    }

    /// <summary>Leva uma linha de outra seção à ficha do cliente, trocando de seção no caminho.</summary>
    /// <remarks>
    /// Nem o painel nem a tela de Processos sabem abrir ficha — quem sabe é a seção
    /// Clientes. Por isso eles avisam a janela, que navega e só então pede a ficha.
    /// </remarks>
    private void AbrirFichaDoCliente(int clienteId)
    {
        Navegar(SecaoApp.Clientes);

        if (Conteudo is ClientesViewModel clientes)
        {
            clientes.AbrirFichaDe(clienteId);
        }
    }
}
