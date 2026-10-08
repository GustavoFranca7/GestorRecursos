using System.Globalization;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Como os dados do banco viram texto na tela.</summary>
/// <remarks>
/// O banco guarda só os dígitos (CPF e WhatsApp), que é o formato certo para buscar e
/// comparar. A pontuação é assunto de quem exibe, e por isso mora aqui, e não na Core.
/// </remarks>
public static class Formato
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    /// <summary>11 dígitos viram 000.000.000-00.</summary>
    public static string Cpf(string? cpf)
    {
        if (string.IsNullOrWhiteSpace(cpf) || cpf.Length != 11)
        {
            return cpf ?? string.Empty;
        }

        return $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";
    }

    /// <summary>11 dígitos viram (00) 00000-0000; 10 viram (00) 0000-0000.</summary>
    public static string Telefone(string? telefone)
    {
        if (string.IsNullOrWhiteSpace(telefone))
        {
            return string.Empty;
        }

        return telefone.Length switch
        {
            11 => $"({telefone[..2]}) {telefone[2..7]}-{telefone[7..]}",
            10 => $"({telefone[..2]}) {telefone[2..6]}-{telefone[6..]}",
            _ => telefone
        };
    }

    /// <summary>8 dígitos viram 00000-000.</summary>
    public static string Cep(string? cep)
    {
        if (string.IsNullOrWhiteSpace(cep) || cep.Length != 8)
        {
            return cep ?? string.Empty;
        }

        return $"{cep[..5]}-{cep[5..]}";
    }

    /// <summary>7 dígitos viram 0.000.000, igual ao que o cadastro mostra.</summary>
    public static string Rg(string? rg)
    {
        if (string.IsNullOrWhiteSpace(rg) || rg.Length != 7)
        {
            return rg ?? string.Empty;
        }

        return $"{rg[0]}.{rg[1..4]}.{rg[4..]}";
    }

    public static string Dinheiro(decimal valor) => valor.ToString("C", Brasil);

    public static string Data(System.DateTime? data) =>
        data?.ToString("dd/MM/yyyy", Brasil) ?? string.Empty;
}
