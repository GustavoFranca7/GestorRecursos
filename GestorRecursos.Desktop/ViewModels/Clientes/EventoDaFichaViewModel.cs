using System;
using System.Collections.Generic;
using System.Linq;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Uma linha da linha do tempo, já escrita em português.</summary>
/// <remarks>
/// A Core entrega os códigos do banco; a frase se monta aqui, com o <see cref="Rotulos"/>
/// e o <see cref="Formato"/>, pelos mesmos motivos das outras abas: fase, forma e dinheiro
/// não podem aparecer de um jeito aqui e de outro na aba ao lado.
/// </remarks>
public class EventoDaFichaViewModel
{
    public EventoDaFichaViewModel(EventoDoCliente dados)
    {
        Quando = Momento(dados.Quando);
        Titulo = MontarTitulo(dados);
        Detalhe = MontarDetalhe(dados);
        CorDoPonto = Cor(dados);
    }

    public string Quando { get; }
    public string Titulo { get; }
    public string Detalhe { get; }
    public bool TemDetalhe => Detalhe.Length > 0;

    /// <summary>Nome do recurso de cor do ponto, resolvido pelo conversor na tela.</summary>
    public string CorDoPonto { get; }

    // A hora só aparece quando existe. Protocolo e resultado são gravados pela data,
    // e "00:00" ali seria uma precisão que o dado não tem.
    private static string Momento(DateTime quando)
    {
        var dia = quando.Date == DateTime.Today
            ? "Hoje"
            : quando.Date == DateTime.Today.AddDays(-1) ? "Ontem" : Formato.Data(quando);

        return quando.TimeOfDay == TimeSpan.Zero ? dia : $"{dia} · {quando:HH:mm}";
    }

    private static string MontarTitulo(EventoDoCliente dados) => dados.Tipo switch
    {
        TipoEvento.Cadastro => "Cliente cadastrado",
        TipoEvento.Contratacao => $"Contratação criada — {Formato.Dinheiro(dados.Valor ?? 0)}",
        TipoEvento.Pagamento => $"Pagamento lançado — {Formato.Dinheiro(dados.Valor ?? 0)}",
        TipoEvento.Despesa => $"Despesa lançada — {Formato.Dinheiro(dados.Valor ?? 0)}",
        TipoEvento.Protocolo => "Protocolo registrado",
        TipoEvento.Resultado => TituloDoResultado(dados),
        TipoEvento.Etapa => "Etapa concluída",
        _ => "Evento"
    };

    private static string TituloDoResultado(EventoDoCliente dados)
    {
        var veredito = dados.Resultado == "DEFERIDO" ? "deferido" : "indeferido";
        var fase = Rotulos.Fase(dados.Codigo);
        return fase.Length > 0 ? $"Resultado: {veredito} na {fase}" : $"Resultado: {veredito}";
    }

    private static string MontarDetalhe(EventoDoCliente dados)
    {
        var partes = new List<string>();

        switch (dados.Tipo)
        {
            case TipoEvento.Contratacao:
                if (dados.Quantidade is { } quantos)
                {
                    partes.Add(quantos == 1 ? "1 serviço" : $"{quantos} serviços");
                }

                if (dados.Detalhe is { Length: > 0 } servicos) partes.Add(servicos);

                // O desconto só se entende ao lado do valor de onde saiu.
                if (dados.Desconto is > 0)
                {
                    partes.Add($"desconto de {Formato.Dinheiro(dados.Desconto.Value)} "
                               + $"sobre {Formato.Dinheiro(dados.ValorTabela ?? 0)} de tabela");
                }

                break;

            case TipoEvento.Pagamento:
                if (Forma(dados) is { Length: > 0 } forma) partes.Add(forma);
                if (dados.Detalhe is { Length: > 0 } observacao) partes.Add(observacao);
                break;

            case TipoEvento.Protocolo:
                if (dados.Identificacao is { Length: > 0 } alvo) partes.Add(alvo);
                if (Rotulos.Fase(dados.Codigo) is { Length: > 0 } onde) partes.Add(onde);
                if (dados.Detalhe is { Length: > 0 } numero) partes.Add(numero);
                break;

            default:
                if (dados.Identificacao is { Length: > 0 } quem) partes.Add(quem);
                if (dados.Detalhe is { Length: > 0 } texto) partes.Add(texto);
                break;
        }

        return string.Join(" · ", partes.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string Forma(EventoDoCliente dados) =>
        ValorBanco.DeOuNulo<FormaPagamento>(dados.Codigo) is { } pagamento
            ? Rotulos.Forma(pagamento, dados.Quantidade ?? 1)
            : string.Empty;

    // A cor diz, antes da leitura, se o dia foi de entrada, de saída ou de andamento.
    private static string Cor(EventoDoCliente dados) => dados.Tipo switch
    {
        TipoEvento.Pagamento => "Sucesso",
        TipoEvento.Despesa => "Alerta",
        TipoEvento.Contratacao => "Primaria",
        TipoEvento.Protocolo => "Primaria",
        TipoEvento.Resultado => dados.Resultado == "DEFERIDO" ? "Sucesso" : "Aviso",
        _ => "TextoApagado"
    };
}
