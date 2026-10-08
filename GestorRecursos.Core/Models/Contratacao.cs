namespace GestorRecursos.Core.Models;

// O "pedido" fechado com o cliente. O desconto mora aqui porque ela negocia
// pelo conjunto: "20 multas, fecho por tanto".
public class Contratacao
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public DateTime DataContratacao { get; set; }

    /// <summary>Soma dos itens antes do desconto. Guardado separado a pedido dela.</summary>
    public decimal ValorTabela { get; set; }

    public decimal Desconto { get; set; }

    /// <summary>O combinado. É dele que sai o valor em aberto.</summary>
    public decimal ValorFinal { get; set; }

    public SituacaoRegistro Situacao { get; set; } = SituacaoRegistro.Andamento;
    public string? Observacoes { get; set; }

    public List<ItemContratacao> Itens { get; set; } = [];

    // Preenchidos pelas consultas que trazem o financeiro junto. Não são colunas:
    // vêm da view, onde são calculados na hora.
    public string ClienteNome { get; set; } = string.Empty;
    public string ClienteCpf { get; set; } = string.Empty;
    public decimal TotalPago { get; set; }
    public decimal TotalDespesas { get; set; }

    /// <summary>Quanto falta receber. Nunca é campo gravado: campo desatualiza.</summary>
    public decimal EmAberto => ValorFinal - TotalPago;

    public bool Quitado => EmAberto <= 0;
}
