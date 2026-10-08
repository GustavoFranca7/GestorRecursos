namespace GestorRecursos.Core.Models;

// Uma fase do recurso: autuação (onde entra a defesa prévia), JARI e CETRAN.
// É linha, e não coluna, para guardar o histórico de um processo que dura anos.
public class Instancia
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public FaseInstancia Fase { get; set; }
    public SituacaoInstancia Situacao { get; set; } = SituacaoInstancia.Contratado;

    /// <summary>A data que não pode ser perdida. É o coração do trabalho dela.</summary>
    public DateTime? PrazoProtocolo { get; set; }

    public DateTime? DataProtocolo { get; set; }
    public string? NumeroProtocolo { get; set; }

    /// <summary>Muda por fase e por tipo: prefeitura, JARI ou DETRAN.</summary>
    public string? OrgaoDestinatario { get; set; }

    public DateTime? DataResultado { get; set; }
    public ResultadoInstancia? Resultado { get; set; }
    public string? Observacoes { get; set; }

    public bool Protocolado => DataProtocolo.HasValue;

    /// <summary>Dias que faltam para o prazo. Negativo quer dizer prazo vencido.</summary>
    public int? DiasParaPrazo =>
        PrazoProtocolo.HasValue ? (PrazoProtocolo.Value.Date - DateTime.Today).Days : null;

    /// <summary>Vence hoje ou já venceu, e ainda não foi protocolado.</summary>
    public bool Urgente => !Protocolado && DiasParaPrazo is <= 0;
}
