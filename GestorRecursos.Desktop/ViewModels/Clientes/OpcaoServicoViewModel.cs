using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Uma linha da lista de serviços, no primeiro passo da contratação.</summary>
/// <remarks>
/// Nem toda linha é um serviço da tabela. Recurso de multa e defesa de CNH entram como
/// uma opção só, porque a variante certa depende de algo que ainda não se sabe — a
/// gravidade do auto, ou se vai haver indicação de condutor. Escolher "Recurso de multa
/// grave" antes de ver a multa seria pedir à despachante que adivinhasse.
/// </remarks>
public partial class OpcaoServicoViewModel : ObservableObject
{
    /// <summary>Opção que junta as variantes de um fluxo inteiro.</summary>
    public OpcaoServicoViewModel(FluxoServico fluxo, IReadOnlyList<TipoServico> variantes)
    {
        Fluxo = fluxo;
        Variantes = variantes;
        Titulo = fluxo.NomeDoGrupo();
        Grupo = fluxo.Grupo();
        Descricao = DescreverExigencia(fluxo);
        Preco = FaixaDePreco(variantes);
        Detalhe = fluxo == FluxoServico.RecursoDeMulta ? "por multa" : "conforme a variante";
    }

    /// <summary>Opção que é um serviço só da tabela.</summary>
    public OpcaoServicoViewModel(TipoServico servico)
    {
        Fluxo = servico.Fluxo;
        Variantes = [servico];
        Titulo = servico.Nome;
        Grupo = servico.Fluxo.Grupo();
        Preco = servico.ValorTabela > 0 ? Formato.Dinheiro(servico.ValorTabela) : "sem custo";

        var exigencia = DescreverExigencia(servico.Fluxo);
        Descricao = servico.QuantidadeEtapas > 0
            ? $"{exigencia} · {servico.QuantidadeEtapas} etapas de acompanhamento"
            : exigencia;
    }

    public FluxoServico Fluxo { get; }

    /// <summary>Os serviços da tabela que esta opção pode virar.</summary>
    public IReadOnlyList<TipoServico> Variantes { get; }

    public string Titulo { get; }
    public string Descricao { get; }
    public string Grupo { get; }
    public string Preco { get; }
    public string Detalhe { get; } = string.Empty;
    public bool TemDetalhe => Detalhe.Length > 0;

    [ObservableProperty] public partial bool Escolhido { get; set; }

    /// <summary>Casa com o que foi digitado na busca.</summary>
    public bool Combina(string termo) =>
        termo.Length == 0
        || Titulo.Contains(termo, System.StringComparison.OrdinalIgnoreCase)
        || Variantes.Any(v => v.Nome.Contains(termo, System.StringComparison.OrdinalIgnoreCase));

    private static string DescreverExigencia(FluxoServico fluxo) => fluxo switch
    {
        FluxoServico.RecursoDeMulta => "Requer veículo e multas · valor definido pela gravidade de cada auto",
        FluxoServico.Indicacao => "Requer veículo e multa",
        FluxoServico.DefesaCnh => "Requer o processo do DETRAN e as multas da pontuação",
        FluxoServico.SobreVeiculo => "Requer apenas o veículo",
        _ => "Não requer veículo nem multa"
    };

    private static string FaixaDePreco(IReadOnlyList<TipoServico> variantes)
    {
        if (variantes.Count == 0)
        {
            return "sem custo";
        }

        var menor = variantes.Min(v => v.ValorTabela);
        var maior = variantes.Max(v => v.ValorTabela);

        return menor == maior
            ? Formato.Dinheiro(menor)
            : $"{Formato.Dinheiro(menor)} a {Formato.Dinheiro(maior)}";
    }
}
