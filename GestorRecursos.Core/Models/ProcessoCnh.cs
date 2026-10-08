namespace GestorRecursos.Core.Models;

// Suspensão e cassação não têm AIT nem veículo: correm contra o condutor.
// Por isso pendem do cliente, do mesmo jeito que a multa pende do veículo.
public class ProcessoCnh
{
    public int Id { get; set; }
    public int ClienteId { get; set; }

    /// <summary>Número que o DETRAN dá na notificação, como "50227/2026".</summary>
    public string NumeroProcesso { get; set; } = string.Empty;

    public TipoProcessoCnh Tipo { get; set; }

    /// <summary>Pontuação acumulada ou infração específica que gerou o processo.</summary>
    public string? Motivo { get; set; }

    public DateTime? DataNotificacao { get; set; }

    /// <summary>Contado da notificação, e não da data de uma infração.</summary>
    public DateTime? PrazoDefesa { get; set; }

    public string? Portaria { get; set; }
    public string? OrgaoInstaurador { get; set; }

    /// <summary>Multas que formaram a pontuação questionada, quando for por pontos.</summary>
    public List<Multa> MultasRelacionadas { get; set; } = [];
}
