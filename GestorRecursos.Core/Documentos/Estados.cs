namespace GestorRecursos.Core.Documentos;

// A sigla basta na tela; o documento escreve o estado por extenso
// ("no Estado de Santa Catarina"), como no modelo do escritório.
internal static class Estados
{
    private static readonly Dictionary<string, string> PorSigla = new()
    {
        ["AC"] = "Acre",
        ["AL"] = "Alagoas",
        ["AP"] = "Amapá",
        ["AM"] = "Amazonas",
        ["BA"] = "Bahia",
        ["CE"] = "Ceará",
        ["DF"] = "Distrito Federal",
        ["ES"] = "Espírito Santo",
        ["GO"] = "Goiás",
        ["MA"] = "Maranhão",
        ["MT"] = "Mato Grosso",
        ["MS"] = "Mato Grosso do Sul",
        ["MG"] = "Minas Gerais",
        ["PA"] = "Pará",
        ["PB"] = "Paraíba",
        ["PR"] = "Paraná",
        ["PE"] = "Pernambuco",
        ["PI"] = "Piauí",
        ["RJ"] = "Rio de Janeiro",
        ["RN"] = "Rio Grande do Norte",
        ["RS"] = "Rio Grande do Sul",
        ["RO"] = "Rondônia",
        ["RR"] = "Roraima",
        ["SC"] = "Santa Catarina",
        ["SP"] = "São Paulo",
        ["SE"] = "Sergipe",
        ["TO"] = "Tocantins"
    };

    public static string? PorExtenso(string? uf) =>
        uf is not null && PorSigla.TryGetValue(uf.Trim().ToUpperInvariant(), out var nome)
            ? nome
            : null;

    /// <summary>O caminho inverso: "Santa Catarina" devolve "SC".</summary>
    /// <remarks>
    /// Serve à importação das procurações antigas, que escrevem o estado por extenso
    /// porque é assim que o documento o diz. Compara sem acento e sem caixa — o texto
    /// vem digitado à mão e aparece como "Santa Catarina" ou "santa catarina".
    /// </remarks>
    public static string? Sigla(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return null;

        var procurado = Simplificar(nome);
        foreach (var (sigla, extenso) in PorSigla)
        {
            if (Simplificar(extenso) == procurado) return sigla;
        }

        return null;
    }

    private static string Simplificar(string texto)
    {
        var semAcento = texto.Trim().ToLowerInvariant()
            .Normalize(System.Text.NormalizationForm.FormD);

        return new string(semAcento
            .Where(c => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c)
                        != System.Globalization.UnicodeCategory.NonSpacingMark)
            .ToArray());
    }
}
