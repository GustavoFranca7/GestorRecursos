using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace GerenciadorClientes.Desktop.Controls;

// Barrinha fina entre as colunas do cabecalho. Arrastando para os lados, muda a largura
// da coluna. Com PodeRedimensionar="False" vira so uma linha separadora, sem arrastar.
public class DivisorColuna : Border
{
    // Largura da coluna controlada por esta barrinha (TwoWay: o arraste grava no ViewModel).
    public static readonly StyledProperty<double> LarguraProperty =
        AvaloniaProperty.Register<DivisorColuna, double>(nameof(Largura), defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<double> MinimoProperty =
        AvaloniaProperty.Register<DivisorColuna, double>(nameof(Minimo), 60d);

    public static readonly StyledProperty<double> MaximoProperty =
        AvaloniaProperty.Register<DivisorColuna, double>(nameof(Maximo), 600d);

    public static readonly StyledProperty<bool> PodeRedimensionarProperty =
        AvaloniaProperty.Register<DivisorColuna, bool>(nameof(PodeRedimensionar), true);

    private double? _xInicial;      // posicao do mouse quando o arraste comecou (nulo = sem arraste)
    private double _larguraInicial;

    public DivisorColuna()
    {
        // Area de clique de 8 px, com a linha visivel de 1 px no meio.
        Width = 8;
        Background = Brushes.Transparent;
        HorizontalAlignment = HorizontalAlignment.Right;
        Child = new Border { Width = 1, HorizontalAlignment = HorizontalAlignment.Center };
        AtualizarModo();
    }

    public double Largura
    {
        get => GetValue(LarguraProperty);
        set => SetValue(LarguraProperty, value);
    }

    public double Minimo
    {
        get => GetValue(MinimoProperty);
        set => SetValue(MinimoProperty, value);
    }

    public double Maximo
    {
        get => GetValue(MaximoProperty);
        set => SetValue(MaximoProperty, value);
    }

    public bool PodeRedimensionar
    {
        get => GetValue(PodeRedimensionarProperty);
        set => SetValue(PodeRedimensionarProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PodeRedimensionarProperty)
        {
            AtualizarModo();
        }
    }

    // Classe "ativo" para o estilo e cursor de setas so nas barrinhas que arrastam.
    private void AtualizarModo()
    {
        Classes.Set("ativo", PodeRedimensionar);
        Cursor = PodeRedimensionar ? new Cursor(StandardCursorType.SizeWestEast) : Cursor.Default;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!PodeRedimensionar)
        {
            return;
        }

        // Posicao medida na janela inteira: a coluna muda de tamanho durante o arraste,
        // e medir em relacao a ela mesma faria a conta "fugir".
        _xInicial = e.GetPosition(TopLevel.GetTopLevel(this)).X;
        _larguraInicial = Largura;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_xInicial is null)
        {
            return;
        }

        double deslocamento = e.GetPosition(TopLevel.GetTopLevel(this)).X - _xInicial.Value;
        Largura = Math.Clamp(_larguraInicial + deslocamento, Minimo, Maximo);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _xInicial = null;
        e.Pointer.Capture(null);
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _xInicial = null;
    }
}
