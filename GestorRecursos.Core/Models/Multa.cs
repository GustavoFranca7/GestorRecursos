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

/// <summary>O que depende de uma multa, para saber se dá para apagá-la.</summary>
/// <remarks>
/// Multa cadastrada no veículo errado é erro comum e merece conserto. Multa que já virou
/// serviço cobrado, não: apagar deixaria uma contratação cobrando por um recurso cujo
/// objeto sumiu. O banco recusa de qualquer forma — isto é para avisar antes, com nome.
/// </remarks>
public class ResumoExclusaoMulta
{
    /// <summary>Itens de contratação que tratam desta multa.</summary>
    public int Recursos { get; set; }

    /// <summary>Processos de suspensão que contam esta multa na pontuação.</summary>
    public int ProcessosCnh { get; set; }

    public bool PodeApagar => Recursos == 0 && ProcessosCnh == 0;
}
