using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GestorRecursos.Desktop.Servicos;
using GestorRecursos.Desktop.ViewModels;
using GestorRecursos.Desktop.Views;

namespace GestorRecursos.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Ferramenta de quem instala, para criar acesso pelo banco: o SQL não calcula
            // PBKDF2. Não é tela do sistema, por isso só abre pela linha de comando.
            if (desktop.Args?.Contains("--hash") == true)
            {
                var gerador = new JanelaHash { DataContext = new GeradorDeHashViewModel() };
                AreaDeTransferencia.Registrar(gerador);
                desktop.MainWindow = gerador;
            }
            else
            {
                // O sistema abre pela porta. Antes ele entrava direto na lista de
                // clientes, sem perguntar quem estava ali (RNF-01).
                MostrarLogin(desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void MostrarLogin(
        IClassicDesktopStyleApplicationLifetime desktop,
        Window? anterior = null)
    {
        var login = new JanelaLogin();
        login.DataContext = new LoginViewModel(_ => Abrir(desktop, login));

        Trocar(desktop, login, fechar: anterior);
    }

    private static void Abrir(IClassicDesktopStyleApplicationLifetime desktop, Window login)
    {
        // Criada antes de receber o ViewModel: o comando de sair precisa fechar esta
        // janela, e para isso a variável tem de existir quando o comando é montado.
        var principal = new JanelaPrincipal();
        principal.DataContext = new JanelaPrincipalViewModel(
            aoSair: () => MostrarLogin(desktop, principal));

        Trocar(desktop, principal, fechar: login);
    }

    /// <summary>Põe a janela nova no lugar da anterior.</summary>
    /// <remarks>
    /// A nova vira a janela do programa e aparece <b>antes</b> de a anterior fechar:
    /// fechar a última janela aberta encerraria o processo inteiro.
    /// </remarks>
    private static void Trocar(
        IClassicDesktopStyleApplicationLifetime desktop,
        Window nova,
        Window? fechar = null)
    {
        desktop.MainWindow = nova;
        nova.Show();
        fechar?.Close();
    }
}
