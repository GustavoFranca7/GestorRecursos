using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace GerenciadorClientes.Desktop.Views;

public partial class ConfirmacaoExclusaoView : UserControl
{
    public ConfirmacaoExclusaoView()
    {
        InitializeComponent();
    }

    // Ao abrir, o foco vai para CANCELAR (e o Esc passa a funcionar).
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Dispatcher.UIThread.Post(() => this.FindControl<Button>("BotaoCancelar")?.Focus());
    }
}
