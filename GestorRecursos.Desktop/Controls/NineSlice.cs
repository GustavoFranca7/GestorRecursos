using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace GestorRecursos.Desktop.Controls;

// Desenha uma imagem esticavel sem deformar os cantos.
// Corta a arte em 9 partes: os 4 cantos ficam intactos, as bordas e o
// miolo esticam para preencher o tamanho que o controle receber.
public class NineSlice : Control
{
    public static readonly StyledProperty<IImage?> SourceProperty =
        AvaloniaProperty.Register<NineSlice, IImage?>(nameof(Source));

    // Quantos pixels de cada borda da arte ficam intactos.
    public static readonly StyledProperty<double> SliceProperty =
        AvaloniaProperty.Register<NineSlice, double>(nameof(Slice), 8d);

    // Ampliacao da arte na tela: 1 pixel desenhado vira 2 pixels.
    public static readonly StyledProperty<double> EscalaProperty =
        AvaloniaProperty.Register<NineSlice, double>(nameof(Escala), 2d);

    static NineSlice()
    {
        AffectsRender<NineSlice>(SourceProperty, SliceProperty, EscalaProperty);
        AffectsMeasure<NineSlice>(SourceProperty, SliceProperty, EscalaProperty);
    }

    public NineSlice()
    {
        // Sem isso o Avalonia suaviza a imagem e a pixel art vira borrao.
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);
        RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
    }

    public IImage? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public double Slice
    {
        get => GetValue(SliceProperty);
        set => SetValue(SliceProperty, value);
    }

    public double Escala
    {
        get => GetValue(EscalaProperty);
        set => SetValue(EscalaProperty, value);
    }

    // Minimo necessario para os cantos caberem. Assim o botao pode
    // encolher ate o conteudo dele, sem exigir a arte inteira.
    protected override Size MeasureOverride(Size disponivel)
    {
        double minimo = Slice * Escala * 2;
        return new Size(minimo, minimo);
    }

    public override void Render(DrawingContext contexto)
    {
        IImage? arte = Source;
        if (arte is null)
        {
            return;
        }

        double larguraArte = arte.Size.Width;
        double alturaArte = arte.Size.Height;

        double corte = Slice;              // corte medido na arte original
        double corteTela = Slice * Escala; // o mesmo corte, ja ampliado
        double largura = Bounds.Width;
        double altura = Bounds.Height;

        // Posicao e tamanho das 3 faixas, na arte (origem) e na tela (destino).
        double[] origemX = { 0, corte, larguraArte - corte };
        double[] origemLarg = { corte, larguraArte - 2 * corte, corte };
        double[] origemY = { 0, corte, alturaArte - corte };
        double[] origemAlt = { corte, alturaArte - 2 * corte, corte };

        double[] destinoX = { 0, corteTela, largura - corteTela };
        double[] destinoLarg = { corteTela, largura - 2 * corteTela, corteTela };
        double[] destinoY = { 0, corteTela, altura - corteTela };
        double[] destinoAlt = { corteTela, altura - 2 * corteTela, corteTela };

        // Desenha as 9 partes, uma a uma.
        for (int linha = 0; linha < 3; linha++)
        {
            for (int coluna = 0; coluna < 3; coluna++)
            {
                // Se o controle ficou menor que os dois cantos, o miolo some.
                if (destinoLarg[coluna] <= 0 || destinoAlt[linha] <= 0)
                {
                    continue;
                }

                contexto.DrawImage(
                    arte,
                    new Rect(origemX[coluna], origemY[linha], origemLarg[coluna], origemAlt[linha]),
                    new Rect(destinoX[coluna], destinoY[linha], destinoLarg[coluna], destinoAlt[linha]));
            }
        }
    }
}
