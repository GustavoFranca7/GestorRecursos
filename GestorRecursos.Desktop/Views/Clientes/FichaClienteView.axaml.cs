using Avalonia.Controls;
using GestorRecursos.Desktop.ViewModels;

namespace GestorRecursos.Desktop.Views;

public partial class FichaClienteView : UserControl
{
    /// <summary>A que distância do fim a lista já pede o lote seguinte.</summary>
    private const double Antecedencia = 160;

    public FichaClienteView()
    {
        InitializeComponent();
    }

    /// <summary>A lista do lado cresce rolando, em vez de paginar.</summary>
    private void AoRolarVizinhos(object? remetente, ScrollChangedEventArgs argumentos)
    {
        if (remetente is not ScrollViewer rolagem || DataContext is not FichaClienteViewModel ficha)
        {
            return;
        }

        // Pedir antes de encostar no fim evita o solavanco de parar e esperar o banco.
        var faltando = rolagem.Extent.Height - (rolagem.Offset.Y + rolagem.Viewport.Height);

        if (faltando <= Antecedencia)
        {
            ficha.CarregarMaisVizinhosCommand.Execute(null);
        }
    }
}
