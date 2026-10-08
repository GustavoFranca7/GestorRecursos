namespace GestorRecursos.Core.Models;

/// <summary>Tudo que a ficha do cliente mostra de uma vez.</summary>
/// <remarks>
/// A ficha abre com dados de cinco tabelas. Trazê-los juntos, numa conexão só, evita que
/// abrir um cliente vire meia dúzia de idas à nuvem — cada uma com a latência da rede.
/// </remarks>
public class FichaCliente
{
    public Cliente Cliente { get; set; } = new();
    public List<VeiculoDaFicha> Veiculos { get; set; } = [];
    public List<ProcessoDaFicha> Processos { get; set; } = [];
    public ResumoFinanceiroCliente Financeiro { get; set; } = new();
}

/// <summary>Um veículo do cliente, com quantas multas ele tem.</summary>
public class VeiculoDaFicha
{
    public int Id { get; set; }
    public string Placa { get; set; } = string.Empty;
    public string? MarcaModelo { get; set; }
    public string? Cor { get; set; }
    public string? Ano { get; set; }

    /// <summary>PROPRIETARIO ou CONDUTOR.</summary>
    public string Vinculo { get; set; } = string.Empty;

    public int QuantidadeMultas { get; set; }
}

/// <summary>Um recurso em andamento, na fase em que está.</summary>
public class ProcessoDaFicha
{
    public int InstanciaId { get; set; }
    public string Servico { get; set; } = string.Empty;
    public string? Ait { get; set; }
    public string? Placa { get; set; }
    public string? Enquadramento { get; set; }
    public string? Gravidade { get; set; }

    /// <summary>AUTUACAO, JARI ou CETRAN.</summary>
    public string Fase { get; set; } = string.Empty;

    public string Situacao { get; set; } = string.Empty;
    public DateTime? PrazoProtocolo { get; set; }
    public DateTime? DataProtocolo { get; set; }
}

/// <summary>O financeiro do cliente somado, para o resumo da ficha.</summary>
public class ResumoFinanceiroCliente
{
    public decimal TotalContratado { get; set; }
    public decimal TotalPago { get; set; }
    public decimal EmAberto => TotalContratado - TotalPago;
    public DateTime? UltimoPagamento { get; set; }

    /// <summary>Quanto do contratado já foi pago, de 0 a 100.</summary>
    public int PercentualQuitado =>
        TotalContratado <= 0 ? 100 : (int)Math.Round(TotalPago / TotalContratado * 100);
}
