using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
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
            desktop.MainWindow = new JanelaPrincipal
            {
                DataContext = new JanelaPrincipalViewModel(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
