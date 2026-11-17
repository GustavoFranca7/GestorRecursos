namespace GestorRecursos.Core.Models;

/// <summary>De onde o item da agenda veio.</summary>
public enum OrigemDoItem
{
    /// <summary>Linha da tabela AGENDA: alguém marcou isso na mão.</summary>
    Compromisso,

    /// <summary>Prazo de protocolo, lido das INSTANCIAS.</summary>
    Prazo
}

/// <summary>Uma linha da agenda, venha ela da tabela ou dos prazos dos recursos.</summary>
/// <remarks>
/// O prazo de protocolo <b>não</b> é gravado na AGENDA. Ele já existe em
/// <c>INSTANCIAS.PRAZO_PROTOCOLO</c>, e copiá-lo para cá criaria duas verdades sobre a mesma
/// data: protocolar o recurso mudaria uma e deixaria a outra para trás. A agenda lê as duas
/// fontes e junta na hora de montar o mês.
///
/// A coluna <c>AGENDA.INSTANCIA_ID</c> continua existindo para o caso de um dia ser preciso
/// marcar algo a mais sobre uma fase específica — não é ela que traz o prazo.
/// </remarks>
public class ItemDaAgenda
{
    /// <summary>ID na origem: da AGENDA no compromisso, da INSTANCIA no prazo.</summary>
    public int Id { get; set; }

    public OrigemDoItem Origem { get; set; }

    public DateTime Data { get; set; }

    /// <summary>Formato HH:MM. Prazo e tarefa sem hora marcada ficam nulos.</summary>
    public string? Hora { get; set; }

    public TipoCompromisso Tipo { get; set; }
    public string Titulo { get; set; } = string.Empty;

    public int? ClienteId { get; set; }
    public string? ClienteNome { get; set; }

    /// <summary>O AIT ou o número do processo, quando o item é um prazo.</summary>
    public string? Referencia { get; set; }

    /// <summary>Em que pé a fase está, como o banco guarda. Só no prazo.</summary>
    /// <remarks>
    /// Serve para o calendário dizer por que o prazo não pede mais nada: deferido,
    /// indeferido, anulado. Antes a fase encerrada era filtrada fora da consulta e o
    /// item sumia do dia, como se nunca tivesse existido.
    /// </remarks>
    public string? Situacao { get; set; }

    /// <summary>O tipo escrito à mão, quando o tipo escolhido foi "Outro".</summary>
    public string? TipoOutro { get; set; }

    /// <summary>A fase como o banco guarda: AUTUACAO, JARI, CETRAN. Só no prazo.</summary>
    /// <remarks>
    /// Crua de propósito: "AUTUACAO" vira "Autuação" na camada de tela, com os demais
    /// rótulos do sistema, e não aqui.
    /// </remarks>
    public string? Fase { get; set; }

    /// <summary>Protocolado, no prazo; marcado como feito, no compromisso.</summary>
    public bool Concluido { get; set; }

    public Recorrencia Recorrencia { get; set; } = Recorrencia.Nenhuma;
    public string? Observacao { get; set; }

    public bool EhPrazo => Origem == OrigemDoItem.Prazo;

    /// <summary>A fase já terminou: deferida, indeferida, anulada, encerrada.</summary>
    public bool Encerrada => EhPrazo && Core.Data.SituacoesSql.Encerrada(Situacao);

    /// <summary>Prazo que vence hoje ou já venceu, e ainda não foi protocolado (RF-27).</summary>
    public bool Urgente => EhPrazo && !Concluido && Data.Date <= DateTime.Today;

    /// <summary>Repete: concluir este cria o próximo (RF-31).</summary>
    public bool Repete => Origem == OrigemDoItem.Compromisso && Recorrencia != Recorrencia.Nenhuma;
}

/// <summary>O mês inteiro da agenda, com o que o mês tem de anotado.</summary>
public class MesDaAgenda
{
    public int Ano { get; set; }
    public int Mes { get; set; }

    public IReadOnlyList<ItemDaAgenda> Itens { get; set; } = [];

    /// <summary>As anotações do diário que existem no período, por dia.</summary>
    public IReadOnlyDictionary<DateTime, string> Diarios { get; set; } =
        new Dictionary<DateTime, string>();
}
