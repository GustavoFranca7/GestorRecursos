namespace GestorRecursos.Core.Models;

/// <summary>Um prazo de protocolo ainda em aberto, com o contexto de quem é e do quê.</summary>
/// <remarks>
/// A linha é a <b>instância</b>, não o cliente. Na lista de clientes um cliente com três
/// recursos vale uma linha e mostra o prazo mais próximo; aqui cada prazo aparece sozinho,
/// porque o que se protocola é a peça, não a pessoa.
/// </remarks>
public class PrazoDoDia
{
    public int InstanciaId { get; set; }
    public int ClienteId { get; set; }
    public string ClienteNome { get; set; } = string.Empty;
    public string ClienteWhatsapp { get; set; } = string.Empty;

    /// <summary>A data que não pode ser perdida.</summary>
    public DateTime Prazo { get; set; }

    /// <summary>AUTUACAO, JARI ou CETRAN, como o banco guarda.</summary>
    public string Fase { get; set; } = string.Empty;

    public string Servico { get; set; } = string.Empty;

    /// <summary>Para onde a peça vai. Nulo quando ainda não foi definido.</summary>
    public string? Orgao { get; set; }

    /// <summary>De onde o recurso nasceu: o AIT da multa ou o nº do processo da CNH.</summary>
    public string? Origem { get; set; }

    /// <summary>Placa do veículo, quando o recurso é de multa.</summary>
    public string? Placa { get; set; }

    /// <summary>Dias até o prazo. Negativo é prazo vencido.</summary>
    public int Dias => (Prazo.Date - DateTime.Today).Days;
}

/// <summary>Uma contratação com saldo a receber, para a cobrança da manhã (RF-38).</summary>
public class CobrancaDoDia
{
    public int ContratacaoId { get; set; }
    public int ClienteId { get; set; }
    public string ClienteNome { get; set; } = string.Empty;
    public string ClienteWhatsapp { get; set; } = string.Empty;
    public DateTime DataContratacao { get; set; }
    public decimal ValorFinal { get; set; }
    public decimal TotalPago { get; set; }
    public decimal EmAberto { get; set; }

    /// <summary>Quantos dias desde que fechou o serviço. É o que ordena a lista.</summary>
    public int Dias { get; set; }

    /// <summary>Já pagou parte: a cobrança é do que falta, e a tela precisa dizer isso.</summary>
    public bool Parcial => TotalPago > 0;
}

/// <summary>O que o painel mostra ao abrir: os prazos e as cobranças, numa ida ao banco.</summary>
/// <remarks>
/// As duas listas vêm juntas porque são lidas juntas, sempre. Duas chamadas separadas
/// custariam duas conexões à nuvem para pintar a mesma tela.
/// </remarks>
public class VisaoDoDia
{
    public IReadOnlyList<PrazoDoDia> Prazos { get; set; } = [];
    public IReadOnlyList<CobrancaDoDia> Cobrancas { get; set; } = [];

    public int Vencidos => Prazos.Count(p => p.Dias < 0);
    public int Hoje => Prazos.Count(p => p.Dias == 0);

    /// <summary>Vence de hoje até sete dias. Inclui o "hoje" de propósito: é a carga da semana.</summary>
    public int NaSemana => Prazos.Count(p => p.Dias is >= 0 and <= 7);

    public decimal TotalEmAberto => Cobrancas.Sum(c => c.EmAberto);
}
