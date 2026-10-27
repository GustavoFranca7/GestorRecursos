using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Media;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Conversões que a tela precisa e que não valem uma propriedade no ViewModel.</summary>
/// <remarks>
/// São coisas de apresentação: largura do menu, cor do item ativo, qual geometria
/// desenhar. Deixá-las aqui mantém o ViewModel falando de estado, e não de pintura.
/// </remarks>
public static class Conversores
{
    /// <summary>Menu recolhido ou aberto, nas larguras definidas no tema.</summary>
    public static readonly IValueConverter LarguraMenu =
        new FuncValueConverter<bool, double>(retraido =>
            Recurso<double>(retraido ? "MenuLarguraRetraido" : "MenuLargura"));

    /// <summary>Hambúrguer quando recolhido (abre), seta quando aberto (recolhe).</summary>
    public static readonly IValueConverter IconeAlavanca =
        new FuncValueConverter<bool, Geometry?>(retraido =>
            Recurso<Geometry>(retraido ? "IconeMenuHamburguer" : "IconeSetaEsquerda"));

    /// <summary>O que esconde a senha. Caractere nulo mostra o que foi digitado.</summary>
    public static readonly IValueConverter MascaraDaSenha =
        new FuncValueConverter<bool, char>(visivel => visivel ? '\0' : '●');

    /// <summary>O olho do campo de senha: riscado enquanto a senha está à mostra.</summary>
    public static readonly IValueConverter DesenhoDoOlho =
        new FuncValueConverter<bool, Geometry?>(visivel =>
            Recurso<Geometry>(visivel ? "IconeOlhoFechado" : "IconeOlho"));

    /// <summary>Busca uma cor do tema pelo nome guardado no ViewModel.</summary>
    /// <remarks>
    /// É o que permite ao selo dizer "AlertaClaro" em vez de carregar um hexadecimal:
    /// a paleta continua inteira em Styles/Cores.axaml.
    /// </remarks>
    public static readonly IValueConverter Pincel =
        new FuncValueConverter<string?, IBrush?>(chave =>
            string.IsNullOrEmpty(chave) ? null : Recurso<IBrush>(chave));

    /// <summary>Busca a geometria do ícone pelo nome guardado no ViewModel.</summary>
    public static readonly IValueConverter Icone =
        new FuncValueConverter<string?, Geometry?>(chave =>
            string.IsNullOrEmpty(chave) ? null : Recurso<Geometry>(chave));

    // A aparência do item do menu (fundo da pílula, cor do ícone e do texto) saiu daqui
    // e virou estilo, com a classe "ativo" no botão: valor escrito no elemento tem
    // precedência sobre estilo, e isso impedia o item de reagir ao mouse.

    // As medidas abaixo existem para o ícone ficar SEMPRE no mesmo lugar, aberto ou
    // recolhido: centro em 36px, que é o meio da barra retraída (72 / 2). Assim,
    // alternar o menu move o texto e a largura, mas nunca o ícone.
    //
    //   recolhido:  pílula de 14 a 58 (quase um círculo) + recuo 12 = ícone de 26 a 46
    //   aberto:     pílula de 10 a 200                   + recuo 16 = ícone de 26 a 46

    /// <summary>Recuo do conteúdo dentro da pílula, calculado para o ícone não se mover.</summary>
    public static readonly IValueConverter RecuoItemMenu =
        new FuncValueConverter<bool, Thickness>(retraido =>
            retraido ? new Thickness(12, 0, 0, 0) : new Thickness(16, 0, 0, 0));

    /// <summary>Folga dos dois lados: a pílula fica solta na barra, sem encostar nas bordas.</summary>
    public static readonly IValueConverter MargemItemMenu =
        new FuncValueConverter<bool, Thickness>(retraido =>
            retraido ? new Thickness(14, 3, 14, 3) : new Thickness(10, 3, 10, 3));

    /// <summary>A alavanca acompanha: à direita quando aberto, no centro quando recolhido.</summary>
    public static readonly IValueConverter AlinhamentoAlavanca =
        new FuncValueConverter<bool, HorizontalAlignment>(retraido =>
            retraido ? HorizontalAlignment.Center : HorizontalAlignment.Right);

    public static readonly IValueConverter MargemAlavanca =
        new FuncValueConverter<bool, Thickness>(retraido =>
            retraido ? new Thickness(0) : new Thickness(0, 0, 14, 0));

    public static readonly IValueConverter Negar =
        new FuncValueConverter<bool, bool>(valor => !valor);

    public static readonly IValueConverter EhNulo =
        new FuncValueConverter<object?, bool>(valor => valor is null);

    public static readonly IValueConverter NaoEhNulo =
        new FuncValueConverter<object?, bool>(valor => valor is not null);

    /// <summary>Selo de saldo e de multa: vermelho quando há algo pendente, verde quando não.</summary>
    public static readonly IValueConverter FundoDoSaldo =
        new FuncValueConverter<bool, IBrush?>(pendente =>
            pendente ? Recurso<IBrush>("AlertaClaro") : Recurso<IBrush>("SucessoClaro"));

    public static readonly IValueConverter CorDoSaldo =
        new FuncValueConverter<bool, IBrush?>(pendente =>
            pendente ? Recurso<IBrush>("Alerta") : Recurso<IBrush>("Sucesso"));

    /// <summary>Campo sem valor fica apagado e em itálico, para não parecer um dado.</summary>
    public static readonly IValueConverter CorDoCampo =
        new FuncValueConverter<bool, IBrush?>(preenchido =>
            preenchido ? Recurso<IBrush>("Texto") : Recurso<IBrush>("TextoApagado"));

    public static readonly IValueConverter EstiloDoCampo =
        new FuncValueConverter<bool, FontStyle>(preenchido =>
            preenchido ? FontStyle.Normal : FontStyle.Italic);

    /// <summary>Valor a receber chama atenção; quitado fica discreto.</summary>
    public static readonly IValueConverter CorValorEmAberto =
        new FuncValueConverter<bool, IBrush?>(temValor =>
            temValor ? Recurso<IBrush>("Alerta") : Recurso<IBrush>("TextoApagado"));

    /// <summary>Lê um recurso do tema pelo nome.</summary>
    private static T? Recurso<T>(string chave)
    {
        var app = Application.Current;
        if (app is not null && app.TryGetResource(chave, app.ActualThemeVariant, out var valor) && valor is T achado)
        {
            return achado;
        }

        return default;
    }
}
