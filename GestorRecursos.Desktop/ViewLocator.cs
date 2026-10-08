using System;
using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using GestorRecursos.Desktop.ViewModels;

namespace GestorRecursos.Desktop;

/// <summary>Acha a tela do ViewModel pelo nome: ClientesViewModel → ClientesView.</summary>
/// <remarks>
/// A troca vale para o nome do tipo e também para o namespace, então quem renomear um
/// precisa renomear o outro.
///
/// As pastas dentro de Views e ViewModels (Janela, Clientes, Arquivados, Comum) são só
/// arrumação: o namespace continua sendo um por camada. Se um dia as pastas virarem
/// namespace, isto continua funcionando, desde que as duas árvores tenham os mesmos nomes.
/// </remarks>
[RequiresUnreferencedCode(
    "Default implementation of ViewLocator involves reflection which may be trimmed away.",
    Url = "https://docs.avaloniaui.net/docs/concepts/view-locator")]
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;
        
        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }
        
        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
