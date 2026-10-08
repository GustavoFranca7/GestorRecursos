namespace GestorRecursos.Core.Models;

// Cada entrada de dinheiro. São vários por contratação: ela aceita
// pagamento em partes ao longo do tempo.
public class Pagamento
{
    public int Id { get; set; }
    public int ContratacaoId { get; set; }
    public DateTime DataPagamento { get; set; }
    public decimal Valor { get; set; }
    public FormaPagamento Forma { get; set; }

    /// <summary>Só faz sentido no crédito. O valor entra integral para o escritório.</summary>
    public int Parcelas { get; set; } = 1;

    public string? Observacao { get; set; }
}

// Custos do serviço: correio, taxa de DETRAN. Servem para saber o lucro real.
public class Despesa
{
    public int Id { get; set; }
    public int ContratacaoId { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public DateTime DataDespesa { get; set; }
}

// Devolução que fica como crédito para o cliente usar depois.
public class Credito
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public decimal Valor { get; set; }
    public string? Origem { get; set; }
    public bool Utilizado { get; set; }
    public DateTime DataCredito { get; set; }
}
