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

    public FaseViewModel(FaseInstancia fase, Instancia? dados, bool servicoCancelado)
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
        //
        // Anular vale também antes do protocolo: é justamente o caso de a fase cair sem
        // que nada tenha sido apresentado — a autuação some e o processo vai direto à
        // JARI. Julgar, não: sem peça protocolada não há o que o órgão decidisse.
        var viva = !Cancelada && !Julgada && !servicoCancelado;
        PodeProtocolar = viva && !Protocolada;
        PodeJulgar = viva && Protocolada;
        PodeAnular = viva && !Protocolada;
    }

    /// <summary>O serviço dono desta fase, para os comandos saberem de onde vem o clique.</summary>
    public ServicoDaFichaViewModel? Servico { get; internal set; }

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

    /// <summary>A fase caiu sem chegar a ser protocolada.</summary>
    public bool PodeAnular { get; }

    public bool TemBotao => PodeProtocolar || PodeJulgar || PodeAnular;

    private static string ExplicacaoDaEspera(FaseInstancia fase) => fase switch
    {
        FaseInstancia.Jari => "Aberta automaticamente em caso de indeferimento na "
                            + "autuação. Sem custo adicional: o valor contratado "
                            + "contempla as três instâncias.",
        FaseInstancia.Cetran => "Última instância administrativa. Aberta em caso de "
                              + "indeferimento na JARI.",
        _ => "Fase ainda não registrada."
    };

    private static (string, string) CoresDaSituacao(Instancia dados) => dados.Situacao switch
    {
        SituacaoInstancia.Deferido => ("SucessoClaro", "Sucesso"),
        SituacaoInstancia.Indeferido => ("AlertaClaro", "Alerta"),

        // Anulada não é derrota: o processo continua. Cor de aviso, não de alarme.
        SituacaoInstancia.Anulado => ("AvisoClaro", "Aviso"),

        SituacaoInstancia.PrazoPerdido => ("AlertaClaro", "AlertaEscuro"),
        SituacaoInstancia.Cancelado or SituacaoInstancia.Desistencia => ("Fundo", "TextoApagado"),
        SituacaoInstancia.Protocolado or SituacaoInstancia.AguardandoJulgamento => ("RoxoClaro", "Roxo"),
        _ => ("AvisoClaro", "Aviso")
    };
}

/// <summary>Um passo do roteiro de um serviço direto.</summary>
/// <remarks>
/// Duas datas, e elas respondem perguntas diferentes: o prazo é até quando precisa estar
/// feito — é dele que sai o alarme, e é ele que vai para a agenda —, e a conclusão é
/// quando foi feito de fato. Marcar a caixa grava hoje; se não foi hoje, o lápis corrige.
/// </remarks>
public partial class EtapaViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public EtapaViewModel(EtapaDoServico dados, bool servicoCancelado)
    {
        Dados = dados;
        Id = dados.Id;
        Ordem = dados.Ordem;
        Nome = dados.Nome;
        Concluida = dados.Concluida;
        PodeMarcar = !servicoCancelado;

        Quando = dados.DataConclusao is { } data ? Formato.Data(data) : "—";

        // O prazo só grita enquanto a etapa está aberta. Depois de cumprida, ele é
        // história: vermelho em história é alarme falso.
        if (dados.Prazo is { } prazo)
        {
            Prazo = Formato.Data(prazo);

            if (!dados.Concluida && !servicoCancelado)
            {
                var dias = (prazo.Date - DateTime.Today).Days;
                Aviso = Prazos.Texto(dias);
                (FundoPrazo, CorPrazo) = Prazos.Cores(dias);
                Urgente = Prazos.Urgente(dias);
            }
            else
            {
                Aviso = string.Empty;
            }
        }
        else
        {
            Prazo = string.Empty;
            Aviso = string.Empty;
        }
    }

    public EtapaDoServico Dados { get; }

    public int Id { get; }
    public int Ordem { get; }
    public string Nome { get; }

    /// <summary>Marcada na tela. O banco é atualizado pelo comando da ficha.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial bool Concluida { get; set; }

    /// <summary>A data que a linha mostra. Observável porque muda ao marcar a caixa.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Quando { get; set; }

    public bool TemConclusao => Quando != "—";

    public string Prazo { get; }
    public bool TemPrazo => Prazo.Length > 0;

    /// <summary>"prazo em 5 dias", "prazo vencido". Vazio quando não há o que vigiar.</summary>
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    public partial string Aviso { get; set; }

    public bool TemAviso => Aviso.Length > 0;
    public string FundoPrazo { get; } = "Fundo";
    public string CorPrazo { get; } = "TextoFraco";
    public bool Urgente { get; }

    /// <summary>Leva para a tela o que o banco acabou de gravar.</summary>
    /// <remarks>
    /// Marcar a caixa é gesto solto e repetido: recarregar a ficha a cada clique faria a
    /// lista piscar e tiraria o lugar de onde ela estava lendo. Então a linha se atualiza
    /// sozinha — e isso inclui a data, que antes só aparecia na próxima abertura da aba.
    /// </remarks>
    public void AplicarMarca(bool concluida, DateTime? quando)
    {
        Concluida = concluida;
        Dados.Concluida = concluida;
        Dados.DataConclusao = quando;

        Quando = quando is { } data ? Formato.Data(data) : "—";

        // Etapa cumprida não tem mais prazo a vigiar; reaberta volta a ter.
        Aviso = concluida || Dados.Prazo is not { } prazo || !PodeMarcar
            ? string.Empty
            : Prazos.Texto((prazo.Date - DateTime.Today).Days);

        OnPropertyChanged(nameof(TemConclusao));
        OnPropertyChanged(nameof(TemAviso));
    }

    /// <summary>Serviço encerrado não se mexe mais: o roteiro vira histórico.</summary>
    public bool PodeMarcar { get; }

    /// <summary>O serviço dono desta etapa, para o comando saber de onde vem o clique.</summary>
    public ServicoDaFichaViewModel? Servico { get; internal set; }
}

/// <summary>Um serviço contratado na aba Serviços: o cabeçalho e o andamento dele.</summary>
/// <remarks>
/// O mesmo cartão serve aos dois formatos. Recurso mostra as três fases lado a lado;
/// serviço direto mostra o roteiro de etapas. Quem decide é o que o item tem gravado,
/// e não o nome do serviço.
/// </remarks>
public class ServicoDaFichaViewModel : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public ServicoDaFichaViewModel(ServicoDaFicha dados)
    {
        Dados = dados;
        ItemId = dados.ItemId;
        ContratacaoId = dados.ContratacaoId;
        Cancelado = dados.Situacao == "CANCELADO";
        PorInstancias = dados.PorInstancias;

        Titulo = MontarTitulo(dados);

        Ait = dados.Ait;
        Placa = dados.Placa;
        TemPlaca = !string.IsNullOrWhiteSpace(dados.Placa);
        ContratadoEm = Formato.Data(dados.DataContratacao);
        Orgao = string.IsNullOrWhiteSpace(dados.OrgaoAutuador) ? null : dados.OrgaoAutuador;

        // Com vários itens, o valor é da contratação inteira: o preço por item não é
        // guardado porque ela fecha o pacote, e repartir aqui seria inventar um número.
        Valor = dados.ItensNaContratacao > 1
            ? $"{Formato.Dinheiro(dados.ValorContratacao)} por {dados.ItensNaContratacao} itens"
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
            fase.Servico = this;
        }

        Etapas = [.. dados.Etapas.Select(e => new EtapaViewModel(e, Cancelado))];
        TemEtapas = Etapas.Count > 0;

        foreach (var etapa in Etapas)
        {
            etapa.Servico = this;
        }

        Andamento = MontarAndamento();

        Encerrado = Cancelado
            || (PorInstancias && (dados.Instancias.Any(i => i.Resultado == ResultadoInstancia.Deferido)
                                  || Fases[2].Julgada))
            || (TemEtapas && Etapas.All(e => e.Concluida));
    }

    /// <summary>"2 de 5 etapas". Recontado quando uma caixa é marcada na tela.</summary>
    public void Recontar()
    {
        Andamento = MontarAndamento();
        OnPropertyChanged(nameof(Andamento));
        OnPropertyChanged(nameof(TemAndamento));
    }

    private string MontarAndamento() => TemEtapas
        ? $"{Etapas.Count(e => e.Concluida)} de {Etapas.Count} etapas"
        : string.Empty;

    public ServicoDaFicha Dados { get; }
    public int ItemId { get; }

    /// <summary>De qual contratação este serviço veio. Serve ao filtro do financeiro.</summary>
    public int ContratacaoId { get; }

    public string Titulo { get; }
    public string? Ait { get; }
    public string? Placa { get; }
    public bool TemPlaca { get; }
    public string ContratadoEm { get; }
    public string Valor { get; }
    public string? Orgao { get; }
    public bool TemOrgao => Orgao is not null;

    /// <summary>Recurso anda por fases; serviço direto, por etapas.</summary>
    public bool PorInstancias { get; }
    public bool TemEtapas { get; }

    public IReadOnlyList<FaseViewModel> Fases { get; }
    public IReadOnlyList<EtapaViewModel> Etapas { get; }

    /// <summary>"2 de 5 etapas". Vazio no recurso.</summary>
    public string Andamento { get; private set; }
    public bool TemAndamento => Andamento.Length > 0;

    public bool Cancelado { get; }

    /// <summary>Não há mais o que fazer: deferido, esgotado, roteiro cumprido ou cancelado.</summary>
    public bool Encerrado { get; }

    public bool PodeCancelar => !Encerrado;

    /// <summary>Alguma fase ou etapa com o prazo apertado: é o que puxa o olho na lista.</summary>
    public bool Urgente => Fases.Any(f => f.Urgente) || Etapas.Any(e => e.Urgente);

    private static string MontarTitulo(ServicoDaFicha dados)
    {
        var servico = dados.Servico.ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(dados.Ait))
        {
            return $"{servico} · AIT {dados.Ait}";
        }

        return string.IsNullOrWhiteSpace(dados.NumeroProcessoCnh)
            ? servico
            : $"{servico} · PROCESSO {dados.NumeroProcessoCnh}";
    }

    private static Instancia? Achar(Dictionary<FaseInstancia, Instancia> mapa, FaseInstancia fase) =>
        mapa.TryGetValue(fase, out var instancia) ? instancia : null;
}
