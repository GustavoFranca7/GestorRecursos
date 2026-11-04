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

    /// <summary>Chamado uma vez, quando a janela principal abre.</summary>
    public static void Registrar(TopLevel janela) => _janela = janela;

    /// <summary>Copia e avisa.</summary>
    /// <remarks>
    /// O retorno sai pela <see cref="Notificacoes"/>, a mesma de salvar e importar:
    /// copiar um CPF na listagem e na ficha dá exatamente o mesmo aviso, no mesmo canto
    /// e pelo mesmo tempo, sem uma segunda implementação para divergir.
    ///
    /// O valor vai no texto de propósito: serve para conferir o que foi parar na área de
    /// transferência antes de colar em outro lugar.
    /// </remarks>
    public static async Task CopiarAsync(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto) || _janela?.Clipboard is not { } area)
        {
            return;
        }

        await area.SetTextAsync(texto);
        Notificacoes.Informar($"Copiado: {texto}");
    }
}
