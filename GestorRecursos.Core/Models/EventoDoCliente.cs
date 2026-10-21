namespace GestorRecursos.Core.Models;

/// <summary>O que aconteceu, na linha do tempo do cliente.</summary>
public enum TipoEvento
{
    Cadastro,
    Contratacao,
    Pagamento,
    Despesa,
    Protocolo,
    Resultado,
    Etapa
}

/// <summary>Uma linha da aba Histórico.</summary>
/// <remarks>
/// O evento é <b>deduzido</b> das datas que as próprias tabelas já guardam — contratação,
/// pagamento, despesa, protocolo, resultado, etapa —, e não lido de um registro de
/// auditoria. Por isso aparece o que o escritório fez, e não quem fez nem qual campo foi
/// corrigido: isso só sai com a tabela de eventos e com o login, os dois ainda por fazer.
///
/// Os códigos do banco (fase, resultado, forma) saem daqui crus. Quem exibe traduz pelo
/// <c>Rotulos</c>, para a mesma fase não virar "autuação" aqui e "Autuação" na aba
/// Serviços.
/// </remarks>
public class EventoDoCliente
{
    public DateTime Quando { get; set; }
    public TipoEvento Tipo { get; set; }

    /// <summary>O dinheiro do evento: o combinado, o que entrou, o que saiu.</summary>
    public decimal? Valor { get; set; }

    public decimal? ValorTabela { get; set; }
    public decimal? Desconto { get; set; }

    /// <summary>Serviços da contratação, ou parcelas do pagamento.</summary>
    public int? Quantidade { get; set; }

    /// <summary>AUTUACAO, JARI, CETRAN — ou a forma de pagamento.</summary>
    public string? Codigo { get; set; }

    /// <summary>DEFERIDO ou INDEFERIDO.</summary>
    public string? Resultado { get; set; }

    /// <summary>O que identifica o serviço: AIT com placa, ou o processo de CNH.</summary>
    public string? Identificacao { get; set; }

    /// <summary>Número do protocolo, tipo da despesa, nome da etapa, observação.</summary>
    public string? Detalhe { get; set; }
}
