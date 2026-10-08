using Avalonia.Controls;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.Views;

public partial class JanelaPrincipal : Window
{
    public JanelaPrincipal()
    {
        InitializeComponent();

        // A área de transferência pertence à janela; os ViewModels copiam por aqui.
        AreaDeTransferencia.Registrar(this);
    }
}
