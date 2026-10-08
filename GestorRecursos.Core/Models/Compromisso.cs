namespace GestorRecursos.Core.Models;

// Tudo que tem data: prazo de protocolo, atendimento, prova, vistoria, ida ao
// DETRAN e a produção planejada do dia. Uma entidade só porque, para quem usa,
// é tudo a mesma coisa: algo com data que precisa acontecer.
public class Compromisso
{
    public int Id { get; set; }
    public DateTime Data { get; set; }

    /// <summary>Formato HH:MM. Compromisso sem hora marcada fica nulo.</summary>
    public string? Hora { get; set; }

    public TipoCompromisso Tipo { get; set; }
    public string Titulo { get; set; } = string.Empty;
    public int? ClienteId { get; set; }
    public int? InstanciaId { get; set; }
    public bool Concluido { get; set; }
    public Recorrencia Recorrencia { get; set; } = Recorrencia.Nenhuma;
    public string? Observacao { get; set; }

    /// <summary>Nome do cliente, quando a consulta traz o cadastro junto.</summary>
    public string? ClienteNome { get; set; }

    /// <summary>Todo prazo que vence hoje ou antes é urgente, segundo ela.</summary>
    public bool Urgente => !Concluido && Tipo == TipoCompromisso.Prazo && Data.Date <= DateTime.Today;
}

// Uma anotação por dia: o que aconteceu, ou por que nada foi protocolado.
public class AnotacaoDiaria
{
    public int Id { get; set; }
    public DateTime Data { get; set; }
    public string Anotacao { get; set; } = string.Empty;
}
