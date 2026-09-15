using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;

namespace GerenciadorClientes.Desktop.Controls;

// Aviso rapido que aparece, vai ficando transparente e some sozinho.
// Com o mouse em cima ele para de sumir; ao tirar o mouse, recomeca a contagem.
public class Notificacao : ContentControl
{
    // Texto do aviso: quando chega um texto novo, o aviso aparece.
    // E TwoWay para o proprio controle limpar o texto quando some.
    public static readonly StyledProperty<string?> TextoProperty =
        AvaloniaProperty.Register<Notificacao, string?>(nameof(Texto), defaultBindingMode: BindingMode.TwoWay);

    // 1 s com a cor normal + 2 s ficando transparente = 3 s no total.
    private static readonly TimeSpan TempoParado = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan TempoSumindo = TimeSpan.FromSeconds(2);

    private readonly DispatcherTimer _relogio = new() { Interval = TimeSpan.FromMilliseconds(30) };
    private DateTime _inicio;

    // Usa a aparencia padrao de ContentControl, que so mostra o conteudo.
    protected override Type StyleKeyOverride => typeof(ContentControl);

    public Notificacao()
    {
        IsVisible = false;
        _relogio.Tick += (_, _) => AtualizarOpacidade();
    }

    public string? Texto
    {
        get => GetValue(TextoProperty);
        set => SetValue(TextoProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextoProperty && !string.IsNullOrEmpty(Texto))
        {
            Recomecar();
        }
    }

    // Mostra com a cor normal e zera a contagem.
    private void Recomecar()
    {
        Opacity = 1;
        IsVisible = true;
        _inicio = DateTime.Now;
        _relogio.Start();
    }

    private void AtualizarOpacidade()
    {
        TimeSpan jaSumindo = DateTime.Now - _inicio - TempoParado;
        if (jaSumindo <= TimeSpan.Zero)
        {
            return; // ainda no tempo com a cor normal
        }

        double progresso = jaSumindo / TempoSumindo; // 0 = comecou a sumir, 1 = sumiu
        if (progresso >= 1)
        {
            _relogio.Stop();
            IsVisible = false;
            Texto = null; // libera para a proxima mensagem
            return;
        }

        Opacity = 1 - progresso;
    }

    // Mouse em cima: volta a cor normal e para de sumir.
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        _relogio.Stop();
        Opacity = 1;
    }

    // Mouse saiu: recomeca a contagem do zero.
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (IsVisible)
        {
            Recomecar();
        }
    }
}
