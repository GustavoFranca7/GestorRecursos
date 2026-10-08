using Avalonia;
using System;

namespace GestorRecursos.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions { CompositionMode = [Composicao()] })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

    /// <summary>Como a janela chega à tela no Windows.</summary>
    /// <remarks>
    /// O padrão do Avalonia (WinUIComposition) entrega a janela ao DWM como textura, e em
    /// placas NVIDIA isso produz um quadro rasgado de vez em quando: por um instante a
    /// janela aparece semitransparente, com faixas do que está atrás. Não é desenho nosso,
    /// é a composição — o mesmo artefato aparece em aplicativos Chromium na mesma máquina.
    ///
    /// RedirectionSurface usa a superfície clássica da janela e não passa por esse caminho.
    /// Perdemos janela transparente ou desfoque atrás dela, que este sistema não usa: as
    /// telas sobrepostas são desenhadas dentro da janela e continuam iguais.
    ///
    /// Para comparar sem recompilar: GESTOR_COMPOSICAO=winui, directcomposition ou
    /// redirection (veja o README).
    /// </remarks>
    private static Win32CompositionMode Composicao() =>
        Environment.GetEnvironmentVariable("GESTOR_COMPOSICAO")?.Trim().ToLowerInvariant() switch
        {
            "winui" => Win32CompositionMode.WinUIComposition,
            "directcomposition" => Win32CompositionMode.DirectComposition,
            _ => Win32CompositionMode.RedirectionSurface
        };
}
