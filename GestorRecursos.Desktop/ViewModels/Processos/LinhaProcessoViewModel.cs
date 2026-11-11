using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Uma linha da tela de Processos: um processo, com o cliente na própria linha.</summary>
/// <remarks>
/// Os selos e as contas de prazo nascem prontos, como na lista de clientes: calcular isso
/// em conversor significaria refazer tudo a cada repintura, com a tela rolando.
///
/// As fases vêm do <see cref="ServicoDaFichaViewModel"/>, o mesmo da aba Serviços. Não é
/// economia de código: é garantir que "esta fase pode ser protocolada" responda igual nas
/// duas telas, porque quem registra o protocolo daqui grava na mesma tabela.
/// </remarks>
public partial class LinhaProcessoViewModel : ObservableObject
{
    public LinhaProcessoViewModel(Processo dados)
    {
        Servico = new ServicoDaFichaViewModel(dados.Servico);

        ItemId = dados.Servico.ItemId;
        ClienteId = dados.ClienteId;

        Nome = dados.ClienteNome;
        CpfFormatado = Formato.Cpf(dados.ClienteCpf);
        WhatsappFormatado = Formato.Telefone(dados.ClienteWhatsapp);

        Titulo = dados.Servico.Servico;
        Identificacao = MontarIdentificacao(dados.Servico);
        Placa = dados.Servico.Placa;
        TemPlaca = !string.IsNullOrWhiteSpace(Placa);
        ContratadoEm = dados.Servico.DataContratacao;
        ContratadoEmTexto = Formato.Data(ContratadoEm);

        Encerrado = Servico.Encerrado;
        Urgente = Servico.Urgente;

        // A fase aberta é onde está o trabalho. Só pode haver uma: a seguinte nasce
        // quando a anterior é julgada, e julgada ela deixa de estar aberta.
        var aberta = dados.Servico.Instancias
            .Where(i => i.Resultado is null
                     && i.Situacao is not (SituacaoInstancia.Cancelado or SituacaoInstancia.Desistencia))
            .OrderBy(i => i.Fase)
            .LastOrDefault();

        Protocolada = aberta?.Protocolado == true;
        NumeroProtocolo = aberta?.NumeroProtocolo;
        Orgao = aberta?.OrgaoDestinatario ?? dados.Servico.OrgaoAutuador;

        // A fase em que o processo está: a aberta, ou a última que correu quando acabou.
        FaseAtual = aberta?.Fase
                    ?? dados.Servico.Instancias.OrderBy(i => i.Fase).LastOrDefault()?.Fase;

        DeMulta = dados.Servico.MultaId is not null;
        DeCnh = dados.Servico.ProcessoCnhId is not null;
        Direto = !dados.Servico.PorInstancias;

        PrazoData = Encerrado ? null : CalcularPrazo(dados.Servico, aberta);
        DiasAteOPrazo = PrazoData is { } data ? (data.Date - DateTime.Today).Days : null;

        SeloFase = MontarSeloFase(dados.Servico, aberta);
        SeloSituacao = MontarSeloSituacao(dados.Servico, aberta, Encerrado);
        SeloPrazo = DiasAteOPrazo is { } dias
            ? Prazos.Selo(Prazos.Texto(dias), dias)
            : Prazos.Selo(Encerrado ? "encerrado" : "sem prazo", null);

        Busca = MontarBusca(dados);
    }

    /// <summary>O serviço com as três fases e o roteiro, o mesmo tipo que a ficha usa.</summary>
    public ServicoDaFichaViewModel Servico { get; }

    public int ItemId { get; }
    public int ClienteId { get; }

    public string Nome { get; }
    public string CpfFormatado { get; }
    public string WhatsappFormatado { get; }

    public string Titulo { get; }

    /// <summary>"AIT TST000126" ou "Processo 3123123": de que auto o recurso trata.</summary>
    public string? Identificacao { get; }
    public bool TemIdentificacao => !string.IsNullOrWhiteSpace(Identificacao);

    public string? Placa { get; }
    public bool TemPlaca { get; }

    public string? Orgao { get; }
    public bool TemOrgao => !string.IsNullOrWhiteSpace(Orgao);

    public string? NumeroProtocolo { get; }

    public DateTime ContratadoEm { get; }
    public string ContratadoEmTexto { get; }

    public SeloViewModel SeloFase { get; }
    public SeloViewModel SeloSituacao { get; }
    public SeloViewModel SeloPrazo { get; }

    /// <summary>Dias até o prazo que está correndo. Negativo é vencido; nulo é sem prazo.</summary>
    public int? DiasAteOPrazo { get; }

    /// <summary>A data do prazo que está correndo, para o filtro por período.</summary>
    public DateTime? PrazoData { get; }

    /// <summary>Em que instância o processo está. Nula no serviço direto, que não tem.</summary>
    public FaseInstancia? FaseAtual { get; }

    public bool DeMulta { get; }
    public bool DeCnh { get; }

    /// <summary>Serviço que anda por etapas, e não por instâncias.</summary>
    public bool Direto { get; }

    public bool Encerrado { get; }
    public bool Urgente { get; }
    public bool Protocolada { get; }

    public bool Vencido => !Encerrado && DiasAteOPrazo is < 0;
    public bool AguardandoJulgamento => !Encerrado && Protocolada;

    /// <summary>Tudo que a linha responde a uma busca, em minúsculas e sem pontuação.</summary>
    public string Busca { get; }

    /// <summary>As fases abertas na própria linha. Fechado, o processo é só uma linha.</summary>
    [ObservableProperty]
    public partial bool Aberto { get; set; }

    public static string? MontarIdentificacao(ServicoDaFicha servico)
    {
        if (!string.IsNullOrWhiteSpace(servico.Ait))
        {
            return $"AIT {servico.Ait}";
        }

        return string.IsNullOrWhiteSpace(servico.NumeroProcessoCnh)
            ? null
            : $"Processo {servico.NumeroProcessoCnh}";
    }

    /// <summary>O prazo que está correndo: o da fase aberta, ou o da próxima etapa.</summary>
    /// <remarks>
    /// Recurso anda por fases e serviço direto anda por etapas, mas a pergunta da tela é a
    /// mesma — "o que vence primeiro aqui" —, então os dois caem numa coluna só.
    /// </remarks>
    private static DateTime? CalcularPrazo(ServicoDaFicha servico, Instancia? aberta)
    {
        if (servico.PorInstancias)
        {
            return aberta is { Protocolado: false } ? aberta.PrazoProtocolo : null;
        }

        return servico.Etapas
            .Where(e => !e.Concluida && e.Prazo is not null)
            .OrderBy(e => e.Prazo)
            .FirstOrDefault()?.Prazo;
    }

    /// <summary>Em que instância o processo está — ou quanto do roteiro já foi cumprido.</summary>
    private static SeloViewModel MontarSeloFase(ServicoDaFicha servico, Instancia? aberta)
    {
        if (!servico.PorInstancias)
        {
            var feitas = servico.Etapas.Count(e => e.Concluida);
            return new SeloViewModel($"{feitas} de {servico.Etapas.Count} etapas", "Fundo", "TextoFraco");
        }

        // Sem fase aberta, o selo mostra a última que correu: é lá que a história parou.
        var fase = aberta?.Fase
                   ?? servico.Instancias.OrderBy(i => i.Fase).LastOrDefault()?.Fase;

        return fase is null
            ? new SeloViewModel("sem fase", "Fundo", "TextoApagado")
            : new SeloViewModel(Rotulos.Fase(fase.Value.ToString().ToUpperInvariant()), "RoxoClaro", "Roxo");
    }

    /// <summary>Em que pé o processo está, em duas ou três palavras.</summary>
    /// <remarks>
    /// Texto curto de propósito. O <c>Rotulos.Situacao</c> escreve "aguardando protocolo na
    /// JARI", que é certo na ficha e longo demais numa coluna — e a fase já está no selo ao
    /// lado, então repeti-la aqui gastaria a largura duas vezes.
    /// </remarks>
    private static SeloViewModel MontarSeloSituacao(ServicoDaFicha servico, Instancia? aberta, bool encerrado)
    {
        if (servico.Situacao == "CANCELADO")
        {
            return new SeloViewModel("cancelado", "Fundo", "TextoApagado");
        }

        if (servico.Instancias.Any(i => i.Resultado == ResultadoInstancia.Deferido))
        {
            return new SeloViewModel("deferido", "SucessoClaro", "Sucesso");
        }

        if (encerrado)
        {
            return servico.PorInstancias
                ? new SeloViewModel("esgotado", "AlertaClaro", "Alerta")
                : new SeloViewModel("concluído", "SucessoClaro", "Sucesso");
        }

        if (!servico.PorInstancias)
        {
            return new SeloViewModel("em andamento", "AvisoClaro", "Aviso");
        }

        return aberta is { Protocolado: true }
            ? new SeloViewModel("aguardando julgamento", "RoxoClaro", "Roxo")
            : new SeloViewModel("a protocolar", "AvisoClaro", "Aviso");
    }

    /// <summary>
    /// O texto que a busca varre: nome, CPF com e sem pontos, WhatsApp, serviço, AIT,
    /// placa, número do processo e os protocolos já emitidos.
    /// </summary>
    /// <remarks>
    /// Montado uma vez, na construção. A busca é instantânea a cada tecla, e refazer esta
    /// concatenação a cada letra digitada, para cada linha, seria trabalho à toa.
    /// </remarks>
    private static string MontarBusca(Processo dados)
    {
        var partes = new List<string?>
        {
            dados.ClienteNome,
            dados.ClienteCpf,
            Formato.Cpf(dados.ClienteCpf),
            dados.ClienteWhatsapp,
            Formato.Telefone(dados.ClienteWhatsapp),
            dados.Servico.Servico,
            dados.Servico.Ait,
            dados.Servico.Placa,
            dados.Servico.NumeroProcessoCnh,
            dados.Servico.Enquadramento,
            dados.Servico.OrgaoAutuador
        };

        partes.AddRange(dados.Servico.Instancias.Select(i => i.NumeroProtocolo));
        partes.AddRange(dados.Servico.Instancias.Select(i => i.OrgaoDestinatario));

        return string.Join(" ", partes.Where(p => !string.IsNullOrWhiteSpace(p)))
                     .ToLowerInvariant();
    }
}
