using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>O dia como cabeçalho: "Quinta-feira, 12 de novembro".</summary>
/// <remarks>
/// O ano fica de fora: ele já está no título do mês, logo acima, e repetido em cada
/// grupo da lista só ocuparia a linha sem dizer nada novo.
/// </remarks>
public static class CabecalhoDoDia
{
    private static readonly System.Globalization.CultureInfo Brasil = new("pt-BR");

    public static string De(DateTime data)
    {
        var texto = data.ToString("dddd, d 'de' MMMM", Brasil);

        return char.ToUpper(texto[0], Brasil) + texto[1..];
    }
}

/// <summary>Um item na agenda: um prazo que vence ou um compromisso marcado.</summary>
public partial class ItemDaAgendaViewModel : ObservableObject
{
    public ItemDaAgendaViewModel(ItemDaAgenda dados)
    {
        Dados = dados;
        Concluido = dados.Concluido;

        Busca = string.Join(
            ' ',
            Titulo,
            dados.ClienteNome,
            dados.Referencia,
            Rotulo).ToLowerInvariant();
    }

    public ItemDaAgenda Dados { get; }

    public int Id => Dados.Id;
    public DateTime Data => Dados.Data;
    public bool EhPrazo => Dados.EhPrazo;
    public int? ClienteId => Dados.ClienteId;
    public string Busca { get; }

    /// <summary>Muda com o clique na caixinha, sem esperar a recarga do mês.</summary>
    [ObservableProperty]
    public partial bool Concluido { get; set; }

    /// <summary>Prazo se fecha protocolando o recurso, não com um clique aqui.</summary>
    /// <remarks>
    /// Marcar o prazo como feito sem registrar o protocolo criaria um "feito" que o
    /// resto do sistema desconhece: a ficha continuaria cobrando a data, e o processo
    /// seguiria sem protocolo. Por isso a caixinha só existe no compromisso, e clicar
    /// num prazo leva à ficha do cliente, que é onde o protocolo se registra.
    /// </remarks>
    public bool PodeConcluir => !EhPrazo;

    /// <summary>Prazo vencendo hoje ou já vencido, ainda sem protocolo (RF-27).</summary>
    public bool Urgente => Dados.Urgente;

    public bool Venceu => EhPrazo && !Dados.Concluido && Dados.Data.Date < DateTime.Today;
    public bool VenceHoje => EhPrazo && !Dados.Concluido && Dados.Data.Date == DateTime.Today;

    public string? Hora => Dados.Hora;
    public bool TemHora => !string.IsNullOrWhiteSpace(Dados.Hora);
    public string HoraOuTraco => TemHora ? Dados.Hora! : "—";

    /// <summary>No prazo, a fase e o serviço; no compromisso, o que foi escrito.</summary>
    public string Titulo => EhPrazo ? $"{FaseCurta} · {Dados.Titulo}" : Dados.Titulo;

    public string? ClienteNome => Dados.ClienteNome;
    public bool TemCliente => !string.IsNullOrWhiteSpace(Dados.ClienteNome);

    /// <summary>O que vai na célula do calendário: hora quando tem, senão o cliente.</summary>
    public string Rotulo => EhPrazo
        ? $"{FaseCurta} · {ClienteNome}"
        : TemHora
            ? $"{Dados.Hora} {Dados.Titulo}"
            : Dados.Titulo;

    /// <summary>"CETRAN", "JARI", "Autuação": a fase como se escreve.</summary>
    /// <remarks>
    /// Os rótulos do sistema são minúsculos porque entram no meio de frases; aqui o
    /// nome abre a linha, então vai com inicial maiúscula. JARI e CETRAN são siglas e
    /// não mudam.
    /// </remarks>
    private string FaseCurta
    {
        get
        {
            var fase = Rotulos.Fase(Dados.Fase);

            return fase.Length > 0 ? char.ToUpperInvariant(fase[0]) + fase[1..] : fase;
        }
    }

    /// <summary>A segunda linha no painel e na lista: de quem é, e sobre o quê.</summary>
    public string Detalhe
    {
        get
        {
            var partes = new List<string>();

            if (TemCliente)
            {
                partes.Add(ClienteNome!);
            }

            if (!string.IsNullOrWhiteSpace(Dados.Referencia))
            {
                partes.Add(Dados.Referencia!);
            }

            if (Dados.Repete)
            {
                partes.Add(TextoDaRecorrencia);
            }

            if (!EhPrazo && !string.IsNullOrWhiteSpace(Dados.Observacao))
            {
                partes.Add(Dados.Observacao!);
            }

            return string.Join(" · ", partes);
        }
    }

    public bool TemDetalhe => Detalhe.Length > 0;

    public string TextoDaRecorrencia => Dados.Recorrencia switch
    {
        Recorrencia.Semanal => "repete toda semana",
        Recorrencia.Quinzenal => "repete a cada 15 dias",
        Recorrencia.Mensal => "repete todo mês",
        _ => string.Empty
    };

    public string NomeDoTipo => Dados.Tipo switch
    {
        TipoCompromisso.Prazo => "prazo",
        TipoCompromisso.Atendimento => "atendimento",
        TipoCompromisso.Prova => "prova",
        TipoCompromisso.Vistoria => "vistoria",
        TipoCompromisso.Delegacia => "delegacia",
        TipoCompromisso.Detran => "DETRAN",
        TipoCompromisso.Tarefa => "tarefa",

        // "Outro" é o que a pessoa escreveu: é esse texto que o selo mostra.
        _ => string.IsNullOrWhiteSpace(Dados.TipoOutro) ? "outro" : Dados.TipoOutro!.Trim()
    };

    /// <summary>A fase acabou: deferida, indeferida, encerrada.</summary>
    public bool Encerrada => Dados.Encerrada;

    /// <summary>Como terminou: "deferido", "indeferido", "prazo perdido".</summary>
    public string Desfecho => Rotulos.Situacao(Dados.Situacao);

    /// <summary>Peça entregue, julgamento ainda por vir.</summary>
    /// <remarks>
    /// Precisa de selo próprio: riscado com o selo "prazo" ele ficava igual ao que
    /// ainda não foi feito, e a lista não dizia qual dos dois era.
    /// </remarks>
    public bool Protocolado => EhPrazo && Concluido && !Encerrada;

    /// <summary>
    /// O selo da direita, em ordem de importância: como terminou, se já terminou; que
    /// foi entregue, se foi; o aperto do prazo, se ainda corre; o tipo, no resto.
    /// </summary>
    public string Selo => Encerrada ? Desfecho
        : Protocolado ? "protocolado"
        : Venceu ? "venceu"
        : VenceHoje ? "vence hoje"
        : NomeDoTipo;

    // As cores saem por nome da paleta; quem traduz para pincel é Conversores.Pincel.
    // Prazo resolvido perde o vermelho: ele não pede mais nada de ninguém.
    public string Cor => Urgente ? "Alerta" : EhPrazo && Concluido ? "TextoFraco" : CorDoTipo;
    public string Fundo => Urgente ? "AlertaClaro" : EhPrazo && Concluido ? "Fundo" : FundoDoTipo;

    public string CorDoSelo => Encerrada ? CorDoDesfecho
        : Protocolado ? "Sucesso"
        : Venceu || VenceHoje ? "AlertaEscuro"
        : CorDoTipo;

    public string FundoDoSelo => Encerrada ? FundoDoDesfecho
        : Protocolado ? "SucessoClaro"
        : Venceu || VenceHoje ? "AlertaClaro"
        : FundoDoTipo;

    /// <summary>Ganhou é verde, perdeu é vermelho, saiu do caminho é cinza.</summary>
    private string CorDoDesfecho => Dados.Situacao switch
    {
        "DEFERIDO" => "Sucesso",
        "INDEFERIDO" or "PRAZO_PERDIDO" => "AlertaEscuro",
        _ => "TextoFraco"
    };

    private string FundoDoDesfecho => Dados.Situacao switch
    {
        "DEFERIDO" => "SucessoClaro",
        "INDEFERIDO" or "PRAZO_PERDIDO" => "AlertaClaro",
        _ => "Fundo"
    };

    private string CorDoTipo => Dados.Tipo switch
    {
        TipoCompromisso.Prazo => "Alerta",
        TipoCompromisso.Atendimento => "PrimariaEscura",
        TipoCompromisso.Prova => "Roxo",
        TipoCompromisso.Vistoria or TipoCompromisso.Detran or TipoCompromisso.Delegacia => "Aviso",
        TipoCompromisso.Tarefa => "TextoFraco",
        _ => "TextoFraco"
    };

    private string FundoDoTipo => Dados.Tipo switch
    {
        TipoCompromisso.Prazo => "AlertaClaro",
        TipoCompromisso.Atendimento => "PrimariaClara",
        TipoCompromisso.Prova => "RoxoClaro",
        TipoCompromisso.Vistoria or TipoCompromisso.Detran or TipoCompromisso.Delegacia => "AvisoClaro",
        _ => "Fundo"
    };
}

/// <summary>Uma célula do calendário.</summary>
public partial class DiaDoMesViewModel : ObservableObject
{
    /// <summary>Quantos itens cabem antes do "+N".</summary>
    /// <remarks>
    /// Fixo, e não o que couber: a célula muda de altura conforme o mês tem cinco ou seis
    /// semanas, e um limite calculado faria o mesmo dia mostrar três itens num mês e dois
    /// no outro. Com três, o dia cheio avisa que é cheio e o painel mostra o resto.
    /// </remarks>
    private const int Cabem = 3;

    public DiaDoMesViewModel(DateTime data, bool doMes, IReadOnlyList<ItemDaAgendaViewModel> itens)
    {
        Data = data;
        DoMes = doMes;
        Itens = itens;

        Visiveis = [.. itens.Take(Cabem)];

        var sobra = itens.Count - Visiveis.Count;
        Extras = sobra > 0 ? $"+{sobra}" : string.Empty;

        var prazos = itens.Where(i => i.EhPrazo).ToList();
        TotalDePrazos = prazos.Count;
        PrazosFeitos = prazos.Count(p => p.Dados.Concluido);
    }

    public DateTime Data { get; }
    public bool DoMes { get; }
    public IReadOnlyList<ItemDaAgendaViewModel> Itens { get; }
    public ObservableCollection<ItemDaAgendaViewModel> Visiveis { get; }

    public string Numero => Data.Day.ToString();
    public string Extras { get; }
    public bool TemExtras => Extras.Length > 0;

    public bool Hoje => Data.Date == DateTime.Today;

    /// <summary>Tem prazo vencendo ou vencido: a faixa vermelha da esquerda (RF-27).</summary>
    public bool Urgente => Itens.Any(i => i.Urgente);

    [ObservableProperty]
    public partial bool Escolhido { get; set; }

    public int TotalDePrazos { get; }
    public int PrazosFeitos { get; }
    public bool TemPlano => TotalDePrazos > 0;
    public string PlanoTexto => $"{PrazosFeitos}/{TotalDePrazos}";

    /// <summary>A barrinha da célula, sobre um trilho de 40 pixels.</summary>
    public double LarguraFeita =>
        TotalDePrazos == 0 ? 0 : (double)PrazosFeitos / TotalDePrazos * 40;
}

/// <summary>Um dia na visão de lista, com tudo que ele tem.</summary>
public class DiaDaListaViewModel
{
    public DiaDaListaViewModel(DateTime data, IReadOnlyList<ItemDaAgendaViewModel> itens)
    {
        Data = data;
        Itens = itens;

        Titulo = CabecalhoDoDia.De(data);

        var prazos = itens.Count(i => i.EhPrazo);
        var outros = itens.Count - prazos;

        var partes = new List<string>();

        if (prazos > 0)
        {
            partes.Add(prazos == 1 ? "1 prazo" : $"{prazos} prazos");
        }

        if (outros > 0)
        {
            partes.Add(outros == 1 ? "1 compromisso" : $"{outros} compromissos");
        }

        Resumo = string.Join(" · ", partes);
    }

    public DateTime Data { get; }
    public string Titulo { get; }
    public string Resumo { get; }
    public IReadOnlyList<ItemDaAgendaViewModel> Itens { get; }

    public bool Hoje => Data.Date == DateTime.Today;
    public bool Passou => Data.Date < DateTime.Today;
}
