namespace GestorRecursos.Core.Models;

/// <summary>O que o CTB fixa por gravidade: pontos na carteira e valor da multa.</summary>
/// <remarks>
/// Serve para sugerir o preenchimento, não para impor: o valor pode vir com desconto,
/// multiplicador ou diferente do padrão, e quem digita é quem está com o auto na mão.
///
/// Os valores são os do artigo 258 do CTB. Se mudarem, mudam aqui — e não em cada tela.
///
/// Está no código por enquanto. A ideia combinada é virar tabela no banco, editável por
/// uma tela de configuração, junto dos preços dos serviços: o CTB reajusta, e ela não
/// deveria precisar de um programador para acompanhar.
/// </remarks>
public static class InfracaoCtb
{
    public static int Pontos(GravidadeMulta gravidade) => gravidade switch
    {
        GravidadeMulta.Leve => 3,
        GravidadeMulta.Media => 4,
        GravidadeMulta.Grave => 5,
        GravidadeMulta.Gravissima => 7,
        _ => 0
    };

    public static decimal Valor(GravidadeMulta gravidade) => gravidade switch
    {
        GravidadeMulta.Leve => 88.38m,
        GravidadeMulta.Media => 130.16m,
        GravidadeMulta.Grave => 195.23m,
        GravidadeMulta.Gravissima => 293.47m,
        _ => 0m
    };
}
