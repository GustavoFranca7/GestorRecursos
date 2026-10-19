namespace GestorRecursos.Core.Models;

// A tabela de preços do escritório.
public class TipoServico
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>Verdadeiro para o que percorre autuação, JARI e CETRAN.</summary>
    public bool TemInstancias { get; set; }

    /// <summary>O que o serviço pede no cadastro, e por onde a tela passa por causa disso.</summary>
    public FluxoServico Fluxo { get; set; } = FluxoServico.Outro;

    public decimal ValorTabela { get; set; }
    public bool Ativo { get; set; } = true;

    /// <summary>Quantas etapas o roteiro deste serviço tem, quando ele for um serviço direto.</summary>
    public int QuantidadeEtapas { get; set; }
}

/// <summary>O que o fluxo de um serviço exige antes de falar de dinheiro.</summary>
/// <remarks>
/// Regra de caminho, não dado variável: mora aqui porque é a mesma pergunta em todo lugar
/// que precisa saber se a janela mostra o passo do veículo, o das multas ou nenhum dos dois.
/// </remarks>
public static class Exigencias
{
    /// <summary>Precisa escolher um veículo do cliente.</summary>
    public static bool PedeVeiculo(this FluxoServico fluxo) =>
        fluxo is FluxoServico.RecursoDeMulta or FluxoServico.Indicacao or FluxoServico.SobreVeiculo;

    /// <summary>Precisa marcar multas do veículo escolhido.</summary>
    public static bool PedeMulta(this FluxoServico fluxo) =>
        fluxo is FluxoServico.RecursoDeMulta or FluxoServico.Indicacao;

    /// <summary>Precisa do processo que o DETRAN abriu contra o condutor.</summary>
    public static bool PedeProcessoCnh(this FluxoServico fluxo) =>
        fluxo is FluxoServico.DefesaCnh;

    /// <summary>
    /// A variante certa só se sabe depois: a gravidade da multa escolhe o recurso, e ter
    /// ou não indicação escolhe o preço da defesa. Fluxos assim entram como uma opção só
    /// na lista de serviços; os outros entram um por um.
    /// </summary>
    public static bool AgrupaVariantes(this FluxoServico fluxo) =>
        fluxo is FluxoServico.RecursoDeMulta or FluxoServico.DefesaCnh;

    /// <summary>Onde o serviço aparece na lista de escolha.</summary>
    public static string Grupo(this FluxoServico fluxo) => fluxo switch
    {
        FluxoServico.RecursoDeMulta or FluxoServico.Indicacao => "MULTAS",
        FluxoServico.DefesaCnh or FluxoServico.ServicoCnh => "CNH",
        FluxoServico.SobreVeiculo => "VEÍCULO",
        _ => "OUTROS"
    };

    /// <summary>Nome do caminho quando ele junta várias variantes num item só.</summary>
    public static string NomeDoGrupo(this FluxoServico fluxo) => fluxo switch
    {
        FluxoServico.RecursoDeMulta => "Recurso de multa",
        FluxoServico.DefesaCnh => "Defesa de suspensão ou cassação da CNH",
        _ => string.Empty
    };
}
