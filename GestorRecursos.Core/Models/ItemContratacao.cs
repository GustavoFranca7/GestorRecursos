namespace GestorRecursos.Core.Models;

// Cada serviço dentro da contratação: vinte multas viram vinte itens.
// Um item é sobre uma multa OU sobre um processo de CNH, nunca os dois —
// regra que o banco também garante, por CHECK.
public class ItemContratacao
{
    public int Id { get; set; }
    public int ContratacaoId { get; set; }
    public int TipoServicoId { get; set; }
    public int? MultaId { get; set; }
    public int? ProcessoCnhId { get; set; }
    public SituacaoRegistro Situacao { get; set; } = SituacaoRegistro.Andamento;

    /// <summary>Nome do serviço, trazido junto na consulta para não precisar de outra ida ao banco.</summary>
    public string TipoServicoNome { get; set; } = string.Empty;

    public decimal ValorTabela { get; set; }

    /// <summary>As fases percorridas, quando o serviço tem instâncias.</summary>
    public List<Instancia> Instancias { get; set; } = [];

    public Multa? Multa { get; set; }
    public ProcessoCnh? ProcessoCnh { get; set; }
}
