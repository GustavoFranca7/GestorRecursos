namespace GestorRecursos.Core.Models;

/// <summary>O dinheiro entrou ou saiu.</summary>
public enum TipoDeMovimento
{
    Entrada,
    Saida
}

/// <summary>Uma linha do extrato do mês: um pagamento recebido ou uma despesa paga.</summary>
/// <remarks>
/// Pagamento e despesa viram o mesmo tipo de propósito. São tabelas diferentes porque
/// guardam coisas diferentes — forma e parcelas de um lado, tipo do custo do outro —, mas
/// a pergunta do fechamento é uma só: o que passou pelo caixa neste mês, e em que ordem.
/// </remarks>
public class MovimentoDoCaixa
{
    public int Id { get; set; }
    public TipoDeMovimento Tipo { get; set; }
    public DateTime Quando { get; set; }
    public decimal Valor { get; set; }

    public int ContratacaoId { get; set; }
    public int ClienteId { get; set; }
    public string ClienteNome { get; set; } = string.Empty;

    /// <summary>Como o dinheiro entrou. Nulo na despesa, que não tem forma.</summary>
    public FormaPagamento? Forma { get; set; }

    public int Parcelas { get; set; } = 1;

    /// <summary>O tipo da despesa, ou a observação do pagamento.</summary>
    public string? Descricao { get; set; }
}

/// <summary>O caixa de um mês: o que entrou, o que saiu e o que foi fechado.</summary>
public class FechamentoDoMes
{
    public int Ano { get; set; }
    public int Mes { get; set; }

    public IReadOnlyList<MovimentoDoCaixa> Movimentos { get; set; } = [];

    /// <summary>Contratações fechadas no mês, e o valor delas (RF-49).</summary>
    /// <remarks>
    /// Não é a mesma coisa que o que entrou: serviço fechado em novembro e pago em
    /// dezembro conta aqui em novembro e no caixa de dezembro. Separar os dois é o que
    /// impede de ler "mês fraco" num mês que só recebeu depois.
    /// </remarks>
    public int ContratacoesNoMes { get; set; }

    public decimal ValorContratadoNoMes { get; set; }

    public decimal Entrou => Movimentos.Where(m => m.Tipo == TipoDeMovimento.Entrada).Sum(m => m.Valor);
    public decimal Saiu => Movimentos.Where(m => m.Tipo == TipoDeMovimento.Saida).Sum(m => m.Valor);
    public decimal Liquido => Entrou - Saiu;

    /// <summary>Quanto entrou por forma de pagamento, da maior para a menor.</summary>
    public IReadOnlyList<(FormaPagamento Forma, decimal Valor)> PorForma =>
    [
        .. Movimentos
            .Where(m => m.Tipo == TipoDeMovimento.Entrada && m.Forma is not null)
            .GroupBy(m => m.Forma!.Value)
            .Select(g => (Forma: g.Key, Valor: g.Sum(m => m.Valor)))
            .OrderByDescending(x => x.Valor)
    ];
}

/// <summary>Uma contratação com saldo a receber, com o que já foi pago dela.</summary>
/// <remarks>
/// O painel do dia mostra o mesmo saldo, mas só o número. Aqui entra o histórico — quantas
/// parcelas já vieram e quando foi a última —, porque é isso que decide o tom da cobrança:
/// quem nunca pagou nada e quem está pagando devagar não recebem o mesmo recado.
/// </remarks>
public class CobrancaAberta
{
    public int ContratacaoId { get; set; }
    public int ClienteId { get; set; }
    public string ClienteNome { get; set; } = string.Empty;
    public string ClienteCpf { get; set; } = string.Empty;
    public string ClienteWhatsapp { get; set; } = string.Empty;

    public DateTime DataContratacao { get; set; }
    public decimal ValorFinal { get; set; }
    public decimal TotalPago { get; set; }
    public decimal EmAberto { get; set; }

    /// <summary>Dias desde que o serviço foi fechado. É o que ordena a lista.</summary>
    public int Dias { get; set; }

    public int QuantosPagamentos { get; set; }
    public DateTime? UltimoPagamento { get; set; }

    /// <summary>Os serviços que a contratação cobre, para a cobrança dizer de quê.</summary>
    public string? Servicos { get; set; }

    /// <summary>Já pagou parte: a cobrança é do que falta, e o recado muda por isso.</summary>
    public bool Parcial => TotalPago > 0;

    /// <summary>Nunca pagou nada e já passou de 30 dias.</summary>
    public bool SemNenhumPagamento => TotalPago <= 0;
}
