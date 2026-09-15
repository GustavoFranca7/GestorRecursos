using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace GerenciadorClientes.Desktop.Views;

public partial class NovoClienteView : UserControl
{
    public NovoClienteView()
    {
        InitializeComponent();
    }

    // Ao abrir o formulario, o cursor ja vai para o campo Nome.
    // FindControl procura pelo Name do XAML, sem depender do campo gerado automaticamente.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("CampoNome")?.Focus());
    }
}
