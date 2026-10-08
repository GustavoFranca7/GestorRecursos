namespace GestorRecursos.Desktop.ViewModels;

/// <summary>A régua de urgência dos prazos, uma só para o sistema inteiro.</summary>
/// <remarks>
/// Existe porque a mesma regra estava escrita em três lugares — na lista, no cabeçalho da
/// ficha e na linha de processo — e elas divergiram: um prazo de 5 dias aparecia amarelo na
/// lista e verde na ficha. Cor de alerta que muda de tela para tela deixa de ser alerta.
/// </remarks>
public static class Prazos
{
    /// <summary>Vencido ou faltando até 3 dias: vermelho.</summary>
    public const int LimiteVermelho = 3;

    /// <summary>De 4 a 7 dias: amarelo. Acima disso, verde.</summary>
    public const int LimiteAmarelo = 7;

    /// <summary>Nomes das cores do tema para os dias que faltam. Nulo = sem prazo aberto.</summary>
    public static (string Fundo, string Texto) Cores(int? diasAteOPrazo) => diasAteOPrazo switch
    {
        null => ("SucessoClaro", "Sucesso"),
        <= LimiteVermelho => ("AlertaClaro", "Alerta"),
        <= LimiteAmarelo => ("AvisoClaro", "Aviso"),
        _ => ("SucessoClaro", "Sucesso")
    };

    /// <summary>Monta o selo já com a cor certa.</summary>
    public static SeloViewModel Selo(string texto, int? diasAteOPrazo)
    {
        var (fundo, cor) = Cores(diasAteOPrazo);
        return new SeloViewModel(texto, fundo, cor);
    }

    /// <summary>Prazo apertado o bastante para a linha ganhar a faixa vermelha.</summary>
    public static bool Urgente(int? diasAteOPrazo) => diasAteOPrazo is <= LimiteVermelho;

    /// <summary>"prazo vencido", "prazo hoje", "prazo em 5 dias"...</summary>
    public static string Texto(int diasAteOPrazo) => diasAteOPrazo switch
    {
        < 0 => "prazo vencido",
        0 => "prazo hoje",
        1 => "prazo amanhã",
        _ => $"prazo em {diasAteOPrazo} dias"
    };
}
