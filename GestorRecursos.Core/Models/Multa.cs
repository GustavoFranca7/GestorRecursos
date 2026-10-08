namespace GestorRecursos.Core.Models;

// A multa pertence ao veículo, e não ao cliente: é o carro que é multado,
// e é pela placa que a informação volta do DETRAN.
public class Multa
{
    public int Id { get; set; }
    public int VeiculoId { get; set; }
    public string Ait { get; set; } = string.Empty;
    public string? OrgaoAutuador { get; set; }
    public string? CodigoOrgao { get; set; }
    public DateTime? DataHoraInfracao { get; set; }
    public string? LocalInfracao { get; set; }

    /// <summary>Artigo do CTB citado no recurso, como "218 I".</summary>
    public string? Enquadramento { get; set; }

    public string? Descricao { get; set; }

    /// <summary>Define o preço do serviço e sustenta a contagem de pontos.</summary>
    public GravidadeMulta? Gravidade { get; set; }

    public int? Pontos { get; set; }
    public decimal? Valor { get; set; }
    public DateTime? PrazoDefesa { get; set; }
    public DateTime? VencimentoBoleto { get; set; }

    /// <summary>Preenchido quando a consulta traz o veículo junto.</summary>
    public Veiculo? Veiculo { get; set; }

    /// <summary>Prazo vencido e sem recurso protocolado é o pior cenário do escritório.</summary>
    public bool PrazoVencido => PrazoDefesa.HasValue && PrazoDefesa.Value.Date < DateTime.Today;
}
