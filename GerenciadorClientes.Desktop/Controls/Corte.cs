using Avalonia.Media;

namespace GerenciadorClientes.Desktop.Controls;

// Corte de texto longo terminando em "...".
// O padrao do Avalonia usa o caractere "…", que a fonte monogram nao tem
// e por isso aparecia como um quadradinho.
public static class Corte
{
    public static TextTrimming TresPontos { get; } = new TextTrailingTrimming("...", false);
}
