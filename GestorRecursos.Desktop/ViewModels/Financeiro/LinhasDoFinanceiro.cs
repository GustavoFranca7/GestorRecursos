using System;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Uma linha do extrato do mês: o que entrou ou o que saiu.</summary>
/// <remarks>
/// Os selos e os sinais nascem prontos, como nas outras listas: calcular em conversor
/// significaria refazer a conta a cada repintura, com a tela rolando.
/// </remarks>
public class MovimentoViewModel
{
    public MovimentoViewModel(MovimentoDoCaixa dados)
    {
        Id = dados.Id;
        ClienteId = dados.ClienteId;
        Entrada = dados.Tipo == TipoDeMovimento.Entrada;

        Data = Formato.Data(dados.Quando);
        DiaCurto = Formato.DiaCurto(dados.Quando);
        Nome = dados.ClienteNome;

        // O sinal faz o trabalho da cor para quem lê rápido, e continua funcionando
        // numa impressão em preto e branco.
        Valor = $"{(Entrada ? "+" : "−")} {Formato.Dinheiro(dados.Valor)}";
        CorDoValor = Entrada ? "Sucesso" : "Alerta";

        Descricao = Entrada
            ? dados.Descricao
            : dados.Descricao ?? "despesa";

        TemDescricao = !string.IsNullOrWhiteSpace(Descricao);

        Selo = Entrada && dados.Forma is { } forma
            ? new SeloViewModel(Rotulos.Forma(forma, dados.Parcelas), "RoxoClaro", "Roxo")
            : new SeloViewModel("despesa", "AlertaClaro", "Alerta");
    }

    public int Id { get; }
    public int ClienteId { get; }
    public bool Entrada { get; }

    public string Data { get; }
    public string DiaCurto { get; }
    public string Nome { get; }
    public string Valor { get; }
    public string CorDoValor { get; }

    /// <summary>Observação do pagamento, ou o tipo da despesa.</summary>
    public string? Descricao { get; }
    public bool TemDescricao { get; }

    /// <summary>A forma de pagamento, ou o selo de despesa.</summary>
    public SeloViewModel Selo { get; }
}

/// <summary>Uma linha da aba Cobrança: uma contratação com saldo a receber.</summary>
public class CobrancaViewModel
{
    public CobrancaViewModel(CobrancaAberta dados)
    {
        Dados = dados;

        ContratacaoId = dados.ContratacaoId;
        ClienteId = dados.ClienteId;
        Nome = dados.ClienteNome;
        CpfFormatado = Formato.Cpf(dados.ClienteCpf);
        WhatsappFormatado = Formato.Telefone(dados.ClienteWhatsapp);

        Servicos = dados.Servicos;
        TemServicos = !string.IsNullOrWhiteSpace(Servicos);

        ContratadoEm = Formato.Data(dados.DataContratacao);
        EmAberto = Formato.Dinheiro(dados.EmAberto);
        Contratado = Formato.Dinheiro(dados.ValorFinal);
        Pago = Formato.Dinheiro(dados.TotalPago);

        Historico = MontarHistorico(dados);
        SeloAtraso = MontarSeloAtraso(dados.Dias);

        Busca = string.Join(" ", new[]
        {
            dados.ClienteNome, dados.ClienteCpf, Formato.Cpf(dados.ClienteCpf),
            dados.ClienteWhatsapp, Formato.Telefone(dados.ClienteWhatsapp), dados.Servicos
        }).ToLowerInvariant();
    }

    public CobrancaAberta Dados { get; }

    public int ContratacaoId { get; }
    public int ClienteId { get; }

    public string Nome { get; }
    public string CpfFormatado { get; }
    public string WhatsappFormatado { get; }

    public string? Servicos { get; }
    public bool TemServicos { get; }

    public string ContratadoEm { get; }
    public string EmAberto { get; }
    public string Contratado { get; }
    public string Pago { get; }

    /// <summary>"pagou R$ 300,00 de R$ 400,00 · último em 21/01/2026", ou que nunca pagou.</summary>
    public string Historico { get; }

    public SeloViewModel SeloAtraso { get; }

    /// <summary>Passou de 90 dias sem quitar: ganha a faixa vermelha na borda.</summary>
    public bool Antigo => Dados.Dias > 90;

    public string Busca { get; }

    private static string MontarHistorico(CobrancaAberta dados)
    {
        if (dados.SemNenhumPagamento)
        {
            return "nada pago até agora";
        }

        var inicio = $"pagou {Formato.Dinheiro(dados.TotalPago)} de {Formato.Dinheiro(dados.ValorFinal)}";

        if (dados.QuantosPagamentos > 1)
        {
            inicio += $", em {dados.QuantosPagamentos} vezes";
        }

        return dados.UltimoPagamento is { } ultimo
            ? $"{inicio} · último em {Formato.Data(ultimo)}"
            : inicio;
    }

    /// <summary>Há quanto tempo o serviço foi fechado, na cor do quanto isso já pesa.</summary>
    /// <remarks>
    /// A escala é de cobrança, e não de prazo: aqui ninguém perde processo por atraso de
    /// pagamento, então o vermelho só entra quando a conversa já deveria ter acontecido.
    /// </remarks>
    private static SeloViewModel MontarSeloAtraso(int dias)
    {
        var texto = dias switch
        {
            0 => "fechado hoje",
            1 => "há 1 dia",
            _ => $"há {dias} dias"
        };

        return dias switch
        {
            > 90 => new SeloViewModel(texto, "AlertaClaro", "AlertaEscuro"),
            > 30 => new SeloViewModel(texto, "AvisoClaro", "Aviso"),
            _ => new SeloViewModel(texto, "Fundo", "TextoFraco")
        };
    }
}
