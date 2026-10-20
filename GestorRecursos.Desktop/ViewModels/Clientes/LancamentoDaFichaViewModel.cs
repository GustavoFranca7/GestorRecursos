using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Uma entrada de dinheiro, como aparece na lista da contratação.</summary>
public class PagamentoDaFichaViewModel(Pagamento dados)
{
    public Pagamento Dados { get; } = dados;

    public int Id => Dados.Id;
    public string Quando { get; } = Formato.Data(dados.DataPagamento);
    public string Valor { get; } = Formato.Dinheiro(dados.Valor);
    public string Forma { get; } = Rotulos.Forma(dados.Forma, dados.Parcelas);
    public string? Observacao => Dados.Observacao;
    public bool TemObservacao => !string.IsNullOrWhiteSpace(Dados.Observacao);
}

/// <summary>Uma despesa da contratação: correio, taxa de DETRAN.</summary>
public class DespesaDaFichaViewModel(Despesa dados)
{
    public string Quando { get; } = Formato.Data(dados.DataDespesa);
    public string Tipo { get; } = dados.Tipo;
    public string Valor { get; } = Formato.Dinheiro(dados.Valor);
}

/// <summary>Um serviço dentro da contratação, com o que o identifica.</summary>
/// <remarks>
/// A identificação é o que permite achar o serviço exato quando o cliente tem vários:
/// o AIT com a placa, ou o número do processo de CNH.
/// </remarks>
public class ServicoDoLancamentoViewModel(ServicoDaFicha dados)
{
    public string Nome { get; } = dados.Servico;

    public string Identificacao { get; } = dados switch
    {
        { Ait: { Length: > 0 } ait, Placa: { Length: > 0 } placa } => $"AIT {ait} · placa {placa}",
        { Ait: { Length: > 0 } ait } => $"AIT {ait}",
        { NumeroProcessoCnh: { Length: > 0 } processo } => $"processo {processo}",
        { Placa: { Length: > 0 } placa } => $"placa {placa}",
        _ => string.Empty
    };

    public bool TemIdentificacao => Identificacao.Length > 0;

    /// <summary>Serviço encerrado continua na contratação: o valor dele foi cobrado.</summary>
    public bool Encerrado { get; } = dados.Situacao is "CONCLUIDO" or "CANCELADO";
    public string Estado { get; } = dados.Situacao switch
    {
        "CONCLUIDO" => "concluído",
        "CANCELADO" => "encerrado",
        _ => string.Empty
    };
}

/// <summary>Uma contratação do cliente, com o que já entrou e o que falta.</summary>
/// <remarks>
/// O cartão é por contratação, e não por serviço, porque é assim que o dinheiro é
/// combinado: o desconto vale para o conjunto e o pagamento quita o conjunto. Separar
/// por serviço obrigaria a ratear um valor que nunca foi negociado em partes.
/// </remarks>
public class LancamentoDaFichaViewModel
{
    public LancamentoDaFichaViewModel(
        Contratacao dados,
        IReadOnlyCollection<ServicoDaFicha> servicos,
        IEnumerable<Pagamento> pagamentos,
        IEnumerable<Despesa> despesas)
    {
        Dados = dados;

        Contratado = Formato.Dinheiro(dados.ValorFinal);
        Pago = Formato.Dinheiro(dados.TotalPago);
        EmAberto = Formato.Dinheiro(dados.EmAberto);
        Quando = Formato.Data(dados.DataContratacao);
        Tabela = Formato.Dinheiro(dados.ValorTabela);
        Desconto = Formato.Dinheiro(dados.Desconto);
        Observacoes = dados.Observacoes;

        foreach (var servico in servicos)
        {
            Servicos.Add(new ServicoDoLancamentoViewModel(servico));
        }

        // O título ainda é texto corrido: ele vai para o cabeçalho da janela de pagamento,
        // onde não cabe lista.
        Titulo = servicos.Count > 0
            ? string.Join(" · ", servicos.Select(s => s.Servico).Distinct())
            : "sem serviços lançados";

        foreach (var pagamento in pagamentos.OrderByDescending(p => p.DataPagamento))
        {
            Pagamentos.Add(new PagamentoDaFichaViewModel(pagamento));
        }

        foreach (var despesa in despesas.OrderByDescending(d => d.DataDespesa))
        {
            Despesas.Add(new DespesaDaFichaViewModel(despesa));
            TotalDespesas += despesa.Valor;
        }

        DespesasTexto = Formato.Dinheiro(TotalDespesas);

        // O resultado é o que sobra para o escritório: o combinado menos o que ele gastou.
        Resultado = Formato.Dinheiro(dados.ValorFinal - TotalDespesas);
    }

    public Contratacao Dados { get; }

    public int Id => Dados.Id;
    public string Quando { get; }
    public string Titulo { get; }
    public string Tabela { get; }
    public string Desconto { get; }
    public string Contratado { get; }
    public string Pago { get; }
    public string EmAberto { get; }
    public string DespesasTexto { get; }
    public string Resultado { get; }
    public string? Observacoes { get; }

    public ObservableCollection<ServicoDoLancamentoViewModel> Servicos { get; } = [];
    public ObservableCollection<PagamentoDaFichaViewModel> Pagamentos { get; } = [];
    public ObservableCollection<DespesaDaFichaViewModel> Despesas { get; } = [];

    public decimal TotalDespesas { get; }
    public decimal Aberto => Dados.EmAberto;

    public bool TemServicos => Servicos.Count > 0;
    public bool TemDesconto => Dados.Desconto > 0;
    public bool TemPagamentos => Pagamentos.Count > 0;
    public bool TemDespesas => Despesas.Count > 0;
    public bool TemObservacoes => !string.IsNullOrWhiteSpace(Observacoes);

    public bool Quitado => Dados.Quitado;
    public bool Devendo => !Dados.Quitado;

    public string Selo => Quitado ? "quitado" : "em aberto";
    public string FundoSelo => Quitado ? "SucessoClaro" : "AlertaClaro";
    public string CorSelo => Quitado ? "Sucesso" : "Alerta";

    /// <summary>Quanto do contratado já entrou, de 0 a 100.</summary>
    public int Percentual => Dados.ValorFinal <= 0
        ? 100
        : (int)System.Math.Round(Dados.TotalPago / Dados.ValorFinal * 100);

    public string ResumoPagamentos => Pagamentos.Count == 1
        ? "1 pagamento"
        : $"{Pagamentos.Count} pagamentos";
}
