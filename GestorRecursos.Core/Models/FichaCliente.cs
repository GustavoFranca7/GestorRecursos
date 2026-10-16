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

/// <summary>Um veículo com as multas dele, para a aba Veículos e multas.</summary>
/// <remarks>
/// Não entra na abertura da ficha: as multas são muitas e só interessam a quem abriu a
/// aba. A ficha carrega isto sob demanda, na primeira vez que a aba aparece.
/// </remarks>
public class VeiculoComMultas
{
    public int Id { get; set; }
    public string Placa { get; set; } = string.Empty;
    public string? Renavam { get; set; }
    public string? MarcaModelo { get; set; }
    public string? Cor { get; set; }
    public string? Ano { get; set; }
    public string? Observacao { get; set; }

    /// <summary>PROPRIETARIO ou CONDUTOR.</summary>
    public string Vinculo { get; set; } = string.Empty;

    public List<MultaDaFicha> Multas { get; set; } = [];
}

/// <summary>Uma multa do veículo, com a situação do recurso dela, se houver um.</summary>
public class MultaDaFicha
{
    public int Id { get; set; }
    public string Ait { get; set; } = string.Empty;
    public string? Enquadramento { get; set; }
    public string? Descricao { get; set; }
    public string? LocalInfracao { get; set; }
    public DateTime? DataHoraInfracao { get; set; }
    public string? Gravidade { get; set; }
    public int? Pontos { get; set; }
    public decimal? Valor { get; set; }
    public DateTime? PrazoDefesa { get; set; }

    // Preenchidos só quando a multa virou recurso contratado, pela instância mais recente.
    public string? Fase { get; set; }
    public string? Situacao { get; set; }
    public DateTime? PrazoProtocolo { get; set; }

    /// <summary>Sem recurso, o prazo que vale é o da defesa; com recurso, o do protocolo.</summary>
    public DateTime? PrazoQueVale => PrazoProtocolo ?? PrazoDefesa;
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

/// <summary>Um recurso inteiro na aba Processos: o que foi contratado e as fases dele.</summary>
/// <remarks>
/// Diferente do <see cref="ProcessoDaFicha"/>, que mostra uma fase por linha no resumo:
/// aqui vem o recurso completo, com autuação, JARI e CETRAN lado a lado, porque é assim
/// que ela acompanha — o que já foi julgado explica o que está aberto agora.
/// </remarks>
public class RecursoDaFicha
{
    public int ItemId { get; set; }
    public int ContratacaoId { get; set; }
    public string Servico { get; set; } = string.Empty;

    public string? Ait { get; set; }
    public string? NumeroProcessoCnh { get; set; }
    public string? Placa { get; set; }
    public string? Enquadramento { get; set; }
    public string? Gravidade { get; set; }
    public string? OrgaoAutuador { get; set; }

    public DateTime DataContratacao { get; set; }

    /// <summary>ANDAMENTO, CONCLUIDO ou CANCELADO — do item, não da contratação.</summary>
    public string Situacao { get; set; } = string.Empty;

    /// <summary>O valor fechado da contratação e quantos recursos ela cobre.</summary>
    /// <remarks>
    /// O preço por item não é guardado: ela negocia pelo conjunto. Com um recurso só, o
    /// valor da contratação é o dele; com vários, a tela diz que é um valor repartido,
    /// em vez de inventar uma divisão que ninguém combinou.
    /// </remarks>
    public decimal ValorContratacao { get; set; }

    public int ItensNaContratacao { get; set; }

    public List<Instancia> Instancias { get; set; } = [];
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
