using System;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace GestorRecursos.Desktop.Servicos;

/// <summary>Formata o campo enquanto a pessoa digita e recusa o que não cabe nele.</summary>
/// <remarks>
/// Uso: <c>servicos:Mascara.Padrao="cpf"</c> num TextBox. Padrões disponíveis:
/// cpf, cep, data, telefone, rg, numero e letras (os dois últimos aceitam um limite: "numero:11").
///
/// Formatar só na saída deixaria o campo cru durante a digitação, que é justamente quando
/// a pontuação ajuda a conferir. Aqui o texto já aparece pronto: 900.000.000-99.
///
/// O ViewModel recebe o texto formatado e normaliza na hora de gravar — a tela mostra
/// como se lê, o banco guarda como se busca.
/// </remarks>
public static class Mascara
{
    /// <summary>Nome do padrão, com limite opcional depois de dois-pontos.</summary>
    public static readonly AttachedProperty<string?> PadraoProperty =
        AvaloniaProperty.RegisterAttached<TextBox, string?>("Padrao", typeof(Mascara));

    public static void SetPadrao(TextBox alvo, string? valor) => alvo.SetValue(PadraoProperty, valor);
    public static string? GetPadrao(TextBox alvo) => alvo.GetValue(PadraoProperty);

    /// <summary>Evita que a reformatação dispare outra reformatação.</summary>
    private static readonly AttachedProperty<bool> AjustandoProperty =
        AvaloniaProperty.RegisterAttached<TextBox, bool>("Ajustando", typeof(Mascara));

    static Mascara()
    {
        PadraoProperty.Changed.AddClassHandler<TextBox>((caixa, argumentos) =>
        {
            if (argumentos.NewValue is string)
            {
                // Tunnel: chega antes do TextBox tratar a tecla, que é onde dá para recusar.
                caixa.AddHandler(InputElement.TextInputEvent, Recusar, RoutingStrategies.Tunnel);
                caixa.TextChanged += Formatar;
            }
        });
    }

    private static void Recusar(object? remetente, TextInputEventArgs argumentos)
    {
        if (remetente is not TextBox caixa || GetPadrao(caixa) is not { } padrao)
        {
            return;
        }

        if (argumentos.Text?.Any(c => !Aceita(padrao, c)) == true)
        {
            argumentos.Handled = true;
            return;
        }

        // Campo cheio só recusa quando não há seleção: digitar por cima é troca, não acréscimo.
        if (Limite(padrao) is { } limite
            && Conteudo(padrao, caixa.Text).Length >= limite
            && caixa.SelectionStart == caixa.SelectionEnd)
        {
            argumentos.Handled = true;
        }
    }

    private static void Formatar(object? remetente, TextChangedEventArgs argumentos)
    {
        if (remetente is not TextBox caixa
            || GetPadrao(caixa) is not { } padrao
            || caixa.GetValue(AjustandoProperty))
        {
            return;
        }

        var formatado = Aplicar(padrao, caixa.Text);
        if (formatado == caixa.Text)
        {
            return;
        }

        var texto = caixa.Text ?? string.Empty;
        var cursor = Math.Clamp(caixa.CaretIndex, 0, texto.Length);

        // O cursor se guia pelo conteúdo que ficou à esquerda dele, não pela posição bruta:
        // sem isso, corrigir um dígito no meio do campo jogaria o cursor para o fim.
        var antes = Conteudo(padrao, texto[..cursor]).Length;

        caixa.SetValue(AjustandoProperty, true);
        caixa.Text = formatado;
        caixa.CaretIndex = Posicao(padrao, formatado, antes);
        caixa.SetValue(AjustandoProperty, false);
    }

    /// <summary>Monta o texto pontuado a partir do que já foi digitado.</summary>
    public static string Aplicar(string padrao, string? texto)
    {
        var util = Conteudo(padrao, texto);
        if (Limite(padrao) is { } limite && util.Length > limite)
        {
            util = util[..limite];
        }

        return Nome(padrao) switch
        {
            "cpf" => Montar(util, [3, 3, 3, 2], [".", ".", "-"]),
            "cep" => Montar(util, [5, 3], ["-"]),
            "data" => Montar(util, [2, 2, 4], ["/", "/"]),
            "rg" => Montar(util, [1, 3, 3], [".", "."]),
            "telefone" => Telefone(util),
            _ => util
        };
    }

    /// <summary>Nome do padrão, sem o limite que venha depois de dois-pontos.</summary>
    private static string Nome(string padrao)
    {
        var corte = padrao.IndexOf(':');
        return corte < 0 ? padrao : padrao[..corte];
    }

    /// <summary>Quantos caracteres o campo aceita. Nulo = sem limite.</summary>
    private static int? Limite(string padrao)
    {
        var corte = padrao.IndexOf(':');
        if (corte >= 0 && int.TryParse(padrao[(corte + 1)..], out var informado))
        {
            return informado;
        }

        return Nome(padrao) switch
        {
            "cpf" => 11,
            "cep" => 8,
            "data" => 8,
            "telefone" => 11,
            "rg" => 7,
            "placa" => 7,
            _ => null
        };
    }

    /// <summary>Cada padrão aceita um alfabeto: placa mistura os dois, sigla é letra,
    /// o resto é número.</summary>
    private static bool Aceita(string padrao, char caractere) => Nome(padrao) switch
    {
        "letras" => char.IsLetter(caractere),
        "placa" => char.IsLetterOrDigit(caractere),
        _ => char.IsDigit(caractere)
    };

    /// <summary>Só o que conta como conteúdo, sem a pontuação que a máscara põe.</summary>
    private static string Conteudo(string padrao, string? texto)
    {
        var util = new string((texto ?? string.Empty).Where(c => Aceita(padrao, c)).ToArray());

        // Sigla e placa são sempre maiúsculas: corrigir na entrada poupa a digitação.
        return Nome(padrao) is "letras" or "placa" ? util.ToUpperInvariant() : util;
    }

    /// <summary>Onde o cursor cai depois de passar por tantos caracteres de conteúdo.</summary>
    private static int Posicao(string padrao, string texto, int quantos)
    {
        if (quantos <= 0)
        {
            return 0;
        }

        var contados = 0;
        for (var i = 0; i < texto.Length; i++)
        {
            if (Aceita(padrao, texto[i]) && ++contados == quantos)
            {
                return i + 1;
            }
        }

        return texto.Length;
    }

    /// <summary>O décimo primeiro dígito muda o formato: fixo de 10 e celular de 11.</summary>
    private static string Telefone(string digitos) => digitos.Length <= 10
        ? Montar(digitos, [2, 4, 4], [") ", "-"], abre: "(")
        : Montar(digitos, [2, 5, 4], [") ", "-"], abre: "(");

    private static string Montar(string util, int[] pedacos, string[] separadores, string abre = "")
    {
        if (util.Length == 0)
        {
            return string.Empty;
        }

        var texto = new StringBuilder(abre);
        var posicao = 0;

        for (var i = 0; i < pedacos.Length && posicao < util.Length; i++)
        {
            var quanto = Math.Min(pedacos[i], util.Length - posicao);
            texto.Append(util, posicao, quanto);
            posicao += quanto;

            // O separador só entra depois que o pedaço fechou: ninguém digita "123." sozinho.
            if (posicao < util.Length && i < separadores.Length)
            {
                texto.Append(separadores[i]);
            }
        }

        return texto.ToString();
    }
}
