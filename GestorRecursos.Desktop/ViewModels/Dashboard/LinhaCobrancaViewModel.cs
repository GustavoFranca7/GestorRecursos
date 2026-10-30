using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Uma linha da lista de cobranças do painel (RF-38).</summary>
public class LinhaCobrancaViewModel
{
    /// <summary>Acima disso a dívida deixa de ser recente e a linha se destaca.</summary>
    /// <remarks>
    /// Trinta dias é o limite combinado em 29/10: serviço fechado há um mês sem nenhum
    /// pagamento é cobrança atrasada, não venda nova.
    /// </remarks>
    private const int DiasParaAtrasado = 30;

    public LinhaCobrancaViewModel(CobrancaDoDia dados)
    {
        ClienteId = dados.ClienteId;
        Nome = dados.ClienteNome;
        WhatsappFormatado = Formato.Telefone(dados.ClienteWhatsapp);

        EmAbertoTexto = Formato.Dinheiro(dados.EmAberto);
        DataTexto = Formato.Data(dados.DataContratacao);
        DiasTexto = dados.Dias switch
        {
            0 => "fechado hoje",
            1 => "há 1 dia",
            _ => $"há {dados.Dias} dias"
        };

        // Quem já pagou parte precisa ser cobrado por outro valor: o que falta.
        Pagamento = dados.Parcial
            ? $"pagou {Formato.Dinheiro(dados.TotalPago)} de {Formato.Dinheiro(dados.ValorFinal)}"
            : $"nada pago de {Formato.Dinheiro(dados.ValorFinal)}";

        Atrasado = dados.Dias >= DiasParaAtrasado;
    }

    public int ClienteId { get; }

    public string Nome { get; }
    public string WhatsappFormatado { get; }
    public string EmAbertoTexto { get; }
    public string DataTexto { get; }
    public string DiasTexto { get; }
    public string Pagamento { get; }

    /// <summary>Dívida de mais de um mês: faixa vermelha, como o prazo estourando.</summary>
    public bool Atrasado { get; }
}
