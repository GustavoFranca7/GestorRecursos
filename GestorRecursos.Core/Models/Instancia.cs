namespace GestorRecursos.Core.Models;

// Uma fase do recurso: autuação (onde entra a defesa prévia), JARI e CETRAN.
// É linha, e não coluna, para guardar o histórico de um processo que dura anos.
public class Instancia
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public FaseInstancia Fase { get; set; }
    public SituacaoInstancia Situacao { get; set; } = SituacaoInstancia.Contratado;

    /// <summary>A data que não pode ser perdida. É o coração do trabalho dela.</summary>
    public DateTime? PrazoProtocolo { get; set; }

    public DateTime? DataProtocolo { get; set; }
    public string? NumeroProtocolo { get; set; }

    /// <summary>Muda por fase e por tipo: prefeitura, JARI ou DETRAN.</summary>
    public string? OrgaoDestinatario { get; set; }

    public DateTime? DataResultado { get; set; }
    public ResultadoInstancia? Resultado { get; set; }
    public string? Observacoes { get; set; }

    public bool Protocolado => DataProtocolo.HasValue;

    /// <summary>A fase caiu sem julgamento: o processo continua na instância seguinte.</summary>
    public bool Anulada => Resultado == ResultadoInstancia.Anulado;

    /// <summary>Esta fase abre a próxima: perdeu o julgamento, ou foi anulada.</summary>
    public bool AbreProximaFase =>
        Resultado is ResultadoInstancia.Indeferido or ResultadoInstancia.Anulado;

    /// <summary>Dias que faltam para o prazo. Negativo quer dizer prazo vencido.</summary>
    public int? DiasParaPrazo =>
        PrazoProtocolo.HasValue ? (PrazoProtocolo.Value.Date - DateTime.Today).Days : null;

    /// <summary>Vence hoje ou já venceu, e ainda não foi protocolado.</summary>
    public bool Urgente => !Protocolado && DiasParaPrazo is <= 0;

    /// <summary>Em que pé a fase está, deduzido do que foi preenchido.</summary>
    /// <remarks>
    /// A situação não é escolhida por ninguém: ela é consequência. Houve resultado, está
    /// julgada; houve protocolo, está aguardando julgamento; não houve nada, está
    /// esperando o protocolo. Deixar alguém escolher abriria a porta para o estado
    /// impossível — "aguardando protocolo" com número de protocolo preenchido.
    /// </remarks>
    public SituacaoInstancia SituacaoPeloPreenchido() => Resultado switch
    {
        ResultadoInstancia.Deferido => SituacaoInstancia.Deferido,
        ResultadoInstancia.Indeferido => SituacaoInstancia.Indeferido,
        ResultadoInstancia.Anulado => SituacaoInstancia.Anulado,
        _ when DataProtocolo.HasValue => SituacaoInstancia.AguardandoJulgamento,
        _ => Fase switch
        {
            FaseInstancia.Jari => SituacaoInstancia.AguardandoProtocoloJari,
            FaseInstancia.Cetran => SituacaoInstancia.AguardandoProtocoloCetran,
            _ => SituacaoInstancia.Contratado
        }
    };

    /// <summary>Tem alguma coisa preenchida além da fase em si.</summary>
    public bool TemConteudo =>
        PrazoProtocolo.HasValue || DataProtocolo.HasValue || DataResultado.HasValue
        || Resultado is not null
        || !string.IsNullOrWhiteSpace(NumeroProtocolo)
        || !string.IsNullOrWhiteSpace(OrgaoDestinatario)
        || !string.IsNullOrWhiteSpace(Observacoes);
}
