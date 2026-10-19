using System;
using System.Collections.Generic;
using System.Linq;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Uma das três colunas do recurso: autuação, JARI ou CETRAN.</summary>
/// <remarks>
/// A coluna existe mesmo quando a fase ainda não começou. É o que deixa claro para o
/// cliente, e para ela, que o valor pago cobre as três e que o caminho continua.
/// </remarks>
public class FaseViewModel
{
    private static readonly string[] Nomes = ["AUTUAÇÃO", "JARI", "CETRAN"];

    public FaseViewModel(FaseInstancia fase, Instancia? dados, bool recursoCancelado)
    {
        Fase = fase;
        Numero = (int)fase + 1;
        Nome = Nomes[(int)fase];
        Dados = dados;

        if (dados is null)
        {
            Situacao = "não iniciada";
            FundoSituacao = "Fundo";
            CorSituacao = "TextoApagado";
            Explicacao = ExplicacaoDaEspera(fase);
            return;
        }

        Id = dados.Id;
        Julgada = dados.Resultado is not null;
        Cancelada = dados.Situacao is SituacaoInstancia.Cancelado or SituacaoInstancia.Desistencia;
        Protocolada = dados.DataProtocolo.HasValue;

        Situacao = Rotulos.Situacao(dados.Situacao.Texto());
        (FundoSituacao, CorSituacao) = CoresDaSituacao(dados);

        Protocolo = dados.NumeroProtocolo;
        Destinatario = dados.OrgaoDestinatario;
        EnviadoEm = dados.DataProtocolo is { } envio ? Formato.Data(envio) : null;
        ResultadoEm = dados.DataResultado is { } julgamento ? Formato.Data(julgamento) : null;

        // O prazo só grita enquanto houver o que protocolar. Depois do protocolo ou do
        // julgamento ele é história, e vermelho em história é alarme falso.
        if (!Protocolada && !Julgada && !Cancelada && dados.PrazoProtocolo is { } prazo)
        {
            var dias = (prazo.Date - DateTime.Today).Days;
            Prazo = $"{Formato.Data(prazo)} · {Prazos.Texto(dias)}";
            (FundoPrazo, CorPrazo) = Prazos.Cores(dias);
            Urgente = Prazos.Urgente(dias);
        }

        // Os botões só aparecem na fase que está realmente aberta.
        var viva = !Cancelada && !Julgada && !recursoCancelado;
        PodeProtocolar = viva && !Protocolada;
        PodeJulgar = viva && Protocolada;
    }

    /// <summary>O recurso dono desta fase, para os comandos saberem de onde vem o clique.</summary>
    public RecursoDaFichaViewModel? Recurso { get; internal set; }

    public FaseInstancia Fase { get; }
    public int Id { get; }
    public int Numero { get; }
    public string Nome { get; }
    public Instancia? Dados { get; }

    public bool Existe => Dados is not null;
    public bool Julgada { get; }
    public bool Cancelada { get; }
    public bool Protocolada { get; }
    public bool Urgente { get; }

    /// <summary>A fase onde está o trabalho de hoje: ela ganha a borda destacada.</summary>
    public bool Atual => Existe && !Julgada && !Cancelada;

    public string Situacao { get; }
    public string FundoSituacao { get; }
    public string CorSituacao { get; }

    public string? Explicacao { get; }
    public bool TemExplicacao => Explicacao is not null;

    public string? Protocolo { get; }
    public bool TemProtocolo => !string.IsNullOrWhiteSpace(Protocolo);

    public string? Destinatario { get; }
    public bool TemDestinatario => !string.IsNullOrWhiteSpace(Destinatario);

    public string? EnviadoEm { get; }
    public bool TemEnvio => EnviadoEm is not null;

    public string? ResultadoEm { get; }
    public bool TemResultado => ResultadoEm is not null;

    public string? Prazo { get; }
    public bool TemPrazo => Prazo is not null;
    public string FundoPrazo { get; } = "Fundo";
    public string CorPrazo { get; } = "TextoFraco";

    public bool PodeProtocolar { get; }
    public bool PodeJulgar { get; }
    public bool TemBotao => PodeProtocolar || PodeJulgar;

    private static string ExplicacaoDaEspera(FaseInstancia fase) => fase switch
    {
        FaseInstancia.Jari => "Abre automaticamente se a autuação indeferir. Sem custo "
                            + "adicional: o valor contratado já inclui as três instâncias.",
        FaseInstancia.Cetran => "Última instância administrativa. Abre se a JARI indeferir.",
        _ => "Ainda não registrada."
    };

    private static (string, string) CoresDaSituacao(Instancia dados) => dados.Situacao switch
    {
        SituacaoInstancia.Deferido => ("SucessoClaro", "Sucesso"),
        SituacaoInstancia.Indeferido => ("AlertaClaro", "Alerta"),
        SituacaoInstancia.PrazoPerdido => ("AlertaClaro", "AlertaEscuro"),
        SituacaoInstancia.Cancelado or SituacaoInstancia.Desistencia => ("Fundo", "TextoApagado"),
        SituacaoInstancia.Protocolado or SituacaoInstancia.AguardandoJulgamento => ("RoxoClaro", "Roxo"),
        _ => ("AvisoClaro", "Aviso")
    };
}

/// <summary>Um recurso inteiro na aba Processos: o cabeçalho e as três fases.</summary>
public class RecursoDaFichaViewModel
{
    public RecursoDaFichaViewModel(RecursoDaFicha dados)
    {
        Dados = dados;
        ItemId = dados.ItemId;
        Cancelado = dados.Situacao == "CANCELADO";

        Titulo = string.IsNullOrWhiteSpace(dados.Ait)
            ? $"{dados.Servico.ToUpperInvariant()} · PROCESSO {dados.NumeroProcessoCnh}"
            : $"{dados.Servico.ToUpperInvariant()} · AIT {dados.Ait}";

        Ait = dados.Ait;
        Placa = dados.Placa;
        TemPlaca = !string.IsNullOrWhiteSpace(dados.Placa);
        ContratadoEm = Formato.Data(dados.DataContratacao);
        Orgao = string.IsNullOrWhiteSpace(dados.OrgaoAutuador) ? null : dados.OrgaoAutuador;

        // Com vários itens, o valor é da contratação inteira: o preço por recurso não é
        // guardado porque ela fecha o pacote, e repartir aqui seria inventar um número.
        Valor = dados.ItensNaContratacao > 1
            ? $"{Formato.Dinheiro(dados.ValorContratacao)} por {dados.ItensNaContratacao} recursos"
            : Formato.Dinheiro(dados.ValorContratacao);

        var porFase = dados.Instancias.ToDictionary(i => i.Fase);
        Fases =
        [
            new FaseViewModel(FaseInstancia.Autuacao, Achar(porFase, FaseInstancia.Autuacao), Cancelado),
            new FaseViewModel(FaseInstancia.Jari, Achar(porFase, FaseInstancia.Jari), Cancelado),
            new FaseViewModel(FaseInstancia.Cetran, Achar(porFase, FaseInstancia.Cetran), Cancelado)
        ];

        foreach (var fase in Fases)
        {
            fase.Recurso = this;
        }

        Encerrado = Cancelado
            || dados.Instancias.Any(i => i.Resultado == ResultadoInstancia.Deferido)
            || Fases[2].Julgada;
    }

    public RecursoDaFicha Dados { get; }
    public int ItemId { get; }

    public string Titulo { get; }
    public string? Ait { get; }
    public string? Placa { get; }
    public bool TemPlaca { get; }
    public string ContratadoEm { get; }
    public string Valor { get; }
    public string? Orgao { get; }
    public bool TemOrgao => Orgao is not null;

    public IReadOnlyList<FaseViewModel> Fases { get; }

    public bool Cancelado { get; }

    /// <summary>Não há mais o que fazer: deferido, esgotado no CETRAN ou cancelado.</summary>
    public bool Encerrado { get; }

    public bool PodeCancelar => !Encerrado;

    /// <summary>Alguma fase com o prazo apertado: é o que puxa o olho na lista.</summary>
    public bool Urgente => Fases.Any(f => f.Urgente);

    private static Instancia? Achar(Dictionary<FaseInstancia, Instancia> mapa, FaseInstancia fase) =>
        mapa.TryGetValue(fase, out var instancia) ? instancia : null;
}
