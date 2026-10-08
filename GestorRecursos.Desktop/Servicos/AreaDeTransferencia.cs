using System;
using System.Threading.Tasks;
using Avalonia.Controls;
// No Avalonia 12 o SetTextAsync é extensão de IClipboard, e não método dele.
using Avalonia.Input.Platform;

namespace GestorRecursos.Desktop.Servicos;

/// <summary>Copiar texto para a área de transferência, de dentro de um ViewModel.</summary>
/// <remarks>
/// No Avalonia a área de transferência pertence à janela, e o ViewModel não conhece janela
/// nenhuma. Em vez de passar o controle para dentro dele, a janela se registra aqui ao
/// abrir — é o mínimo de acoplamento para resolver, sem furar o MVVM em cada tela.
/// </remarks>
public static class AreaDeTransferencia
{
    private static TopLevel? _janela;

    /// <summary>Avisa que algo foi copiado, para a janela mostrar o retorno.</summary>
    /// <remarks>
    /// O aviso é um só, da janela, e não de cada tela: assim copiar um CPF na listagem e
    /// na ficha dá exatamente o mesmo retorno, sem duas implementações para divergir.
    /// </remarks>
    public static event Action<string>? Copiou;

    /// <summary>Chamado uma vez, quando a janela principal abre.</summary>
    public static void Registrar(TopLevel janela) => _janela = janela;

    public static async Task CopiarAsync(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || _janela?.Clipboard is not { } area)
        {
            return;
        }

        await area.SetTextAsync(texto);
        Copiou?.Invoke(texto);
    }
}
