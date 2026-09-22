using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace GestorRecursos.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Tunnel: a janela recebe o Ctrl+C antes de qualquer controle, mesmo sem o texto ter o foco.
        AddHandler(KeyDownEvent, AoApertarTecla, RoutingStrategies.Tunnel);
    }

    // Ctrl+C copia o trecho selecionado na lista, esteja o foco onde estiver.
    private void AoApertarTecla(object? sender, KeyEventArgs e)
    {
        bool ctrlC = e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (!ctrlC)
        {
            return;
        }

        // Procura, entre os textos da tela, o que esta com algum trecho selecionado.
        SelectableTextBlock? selecionado = this.GetVisualDescendants()
            .OfType<SelectableTextBlock>()
            .FirstOrDefault(texto => !string.IsNullOrEmpty(texto.SelectedText));

        if (selecionado is not null)
        {
            selecionado.Copy();
            e.Handled = true;
        }
    }
}
