using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>As duas perguntas de dinheiro do escritório.</summary>
public enum AbaFinanceiro
{
    /// <summary>O que passou pelo caixa num mês.</summary>
    Caixa,

    /// <summary>O que ainda não entrou.</summary>
    Cobranca
}

/// <summary>A seção Financeiro: o caixa do mês e quem está devendo.</summary>
/// <remarks>
/// A aba Financeiro da ficha responde "quanto este cliente deve". Faltavam as duas
/// perguntas do escritório inteiro, e elas são diferentes entre si:
///
/// <b>Caixa</b> é sobre um período fechado — quanto entrou em novembro, quanto saiu em
/// despesa, qual o líquido, e por qual forma o dinheiro veio. É o RF-41, e é o que se olha
/// uma vez por mês.
///
/// <b>Cobrança</b> é sobre o que não aconteceu — quem fechou serviço e não pagou, há
/// quanto tempo, e se já veio alguma parte. O painel do dia mostra o mesmo saldo, mas sem
/// o histórico: quem nunca pagou nada e quem está pagando devagar não recebem o mesmo
/// recado, e essa diferença não cabia num cartão do painel.
///
/// As duas moram na mesma seção porque são o mesmo dinheiro em dois tempos — o que entrou
/// e o que falta entrar —, e porque a resposta de uma leva à outra: lançar o pagamento
/// atrasado aqui faz o mês fechar diferente ao lado.
/// </remarks>
public partial class FinanceiroViewModel : ViewModelBase
{
    private static readonly CultureInfo Brasil = new("pt-BR");

    private readonly FinanceiroDAO _financeiro = new();

    private readonly Action<int>? _aoAbrirCliente;

    private List<CobrancaViewModel> _todasAsCobrancas = [];

    public ObservableCollection<MovimentoViewModel> Movimentos { get; } = [];
    public ObservableCollection<CobrancaViewModel> Cobrancas { get; } = [];

    /// <summary>Quanto entrou por forma de pagamento, no mês escolhido.</summary>
    public ObservableCollection<FatiaDaFormaViewModel> PorForma { get; } = [];

    public FinanceiroViewModel(Action<int>? aoAbrirCliente = null)
    {
        _aoAbrirCliente = aoAbrirCliente;

        var hoje = DateTime.Today;
        Ano = hoje.Year;
        Mes = hoje.Month;

        // No preview do Rider não há banco nem wallet: a tela abriria com erro de conexão.
        if (!Design.IsDesignMode)
        {
            CarregarCommand.Execute(null);
        }
    }

    [ObservableProperty]
    public partial AbaFinanceiro Aba { get; set; } = AbaFinanceiro.Caixa;

    public bool NoCaixa => Aba == AbaFinanceiro.Caixa;
    public bool NaCobranca => Aba == AbaFinanceiro.Cobranca;

    public int Ano { get; private set; }
    public int Mes { get; private set; }

    [ObservableProperty]
    public partial string TituloDoMes { get; set; } = string.Empty;

    /// <summary>Não dá para avançar além do mês corrente: mês futuro não tem caixa.</summary>
    public bool PodeAvancar => new DateTime(Ano, Mes, 1) < new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    /// <summary>O seletor de mês, aberto por cima do título.</summary>
    /// <remarks>
    /// As setas servem para andar de um em um, que é o uso de todo dia. Para voltar a
    /// março do ano passado seriam vinte cliques, e é para isso que existe o seletor.
    /// </remarks>
    [ObservableProperty]
    public partial bool SeletorAberto { get; set; }

    /// <summary>O ano que o seletor mostra. Trocar de ano aqui não recarrega nada.</summary>
    [ObservableProperty]
    public partial int AnoDoSeletor { get; set; }

    public ObservableCollection<OpcaoDeMes> MesesDoSeletor { get; } = [];

    /// <summary>Ano futuro não tem caixa, então o seletor também não passa do ano atual.</summary>
    public bool PodeAvancarAno => AnoDoSeletor < DateTime.Today.Year;

    /// <summary>Já está no mês corrente: o atalho para "hoje" não teria o que fazer.</summary>
    public bool NoMesAtual => Ano == DateTime.Today.Year && Mes == DateTime.Today.Month;

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? Erro { get; set; }

    /// <summary>Formulário de pagamento, por cima da lista. Nulo quando não há.</summary>
    [ObservableProperty]
    public partial ViewModelBase? Formulario { get; set; }

    [ObservableProperty]
    public partial string Entrou { get; set; } = Formato.Dinheiro(0);

    [ObservableProperty]
    public partial string Saiu { get; set; } = Formato.Dinheiro(0);

    [ObservableProperty]
    public partial string Liquido { get; set; } = Formato.Dinheiro(0);

    [ObservableProperty]
    public partial string Fechados { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ValorFechado { get; set; } = Formato.Dinheiro(0);

    [ObservableProperty]
    public partial string TotalEmAberto { get; set; } = Formato.Dinheiro(0);

    [ObservableProperty]
    public partial string ResumoDaCobranca { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TermoBusca { get; set; } = string.Empty;

    public bool TemBusca => TermoBusca.Length > 0;
    public bool SemMovimentos => !Carregando && Movimentos.Count == 0;
    public bool SemCobrancas => !Carregando && Cobrancas.Count == 0;

    public string MensagemSemCobrancas => TemBusca
        ? "Ninguém com esse nome está devendo."
        : "Ninguém está devendo. Tudo que foi contratado já foi pago.";

    [RelayCommand]
    private async Task Carregar()
    {
        Carregando = true;
        Erro = null;

        try
        {
            var fechamento = await _financeiro.FecharMesAsync(Ano, Mes);
            var cobrancas = await _financeiro.ListarCobrancasAsync();

            PreencherCaixa(fechamento);
            PreencherCobranca(cobrancas);
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível carregar o financeiro: {excecao.Message}";
        }
        finally
        {
            Carregando = false;
            OnPropertyChanged(nameof(SemMovimentos));
            OnPropertyChanged(nameof(SemCobrancas));
        }
    }

    private void PreencherCaixa(FechamentoDoMes fechamento)
    {
        TituloDoMes = MesPorExtenso(fechamento.Ano, fechamento.Mes);

        Entrou = Formato.Dinheiro(fechamento.Entrou);
        Saiu = Formato.Dinheiro(fechamento.Saiu);
        Liquido = Formato.Dinheiro(fechamento.Liquido);

        Fechados = fechamento.ContratacoesNoMes == 1
            ? "1 serviço fechado"
            : $"{fechamento.ContratacoesNoMes} serviços fechados";

        ValorFechado = Formato.Dinheiro(fechamento.ValorContratadoNoMes);

        Movimentos.Clear();
        foreach (var movimento in fechamento.Movimentos)
        {
            Movimentos.Add(new MovimentoViewModel(movimento));
        }

        // A barra de cada forma é proporcional à maior do mês, e não ao total: com cinco
        // formas, dividir pelo total deixaria todas rasteiras e ilegíveis.
        var formas = fechamento.PorForma;
        var maior = formas.Count > 0 ? formas.Max(f => f.Valor) : 0m;

        PorForma.Clear();
        foreach (var (forma, valor) in formas)
        {
            PorForma.Add(new FatiaDaFormaViewModel(forma, valor, maior));
        }

        OnPropertyChanged(nameof(PodeAvancar));
    }

    private void PreencherCobranca(List<CobrancaAberta> cobrancas)
    {
        _todasAsCobrancas = [.. cobrancas.Select(c => new CobrancaViewModel(c))];

        TotalEmAberto = Formato.Dinheiro(cobrancas.Sum(c => c.EmAberto));

        var quantos = cobrancas.Count;
        var pessoas = cobrancas.Select(c => c.ClienteId).Distinct().Count();

        ResumoDaCobranca = quantos == 0
            ? string.Empty
            : $"{quantos} {(quantos == 1 ? "contratação" : "contratações")} de " +
              $"{pessoas} {(pessoas == 1 ? "cliente" : "clientes")}";

        AplicarBusca();
    }

    [RelayCommand]
    private void TrocarAba(AbaFinanceiro aba)
    {
        Aba = aba;

        OnPropertyChanged(nameof(NoCaixa));
        OnPropertyChanged(nameof(NaCobranca));
    }

    /// <summary>Volta um mês. Não há limite para trás: o histórico é todo consultável.</summary>
    [RelayCommand]
    private void MesAnterior() => IrPara(-1);

    [RelayCommand]
    private void MesSeguinte()
    {
        if (PodeAvancar)
        {
            IrPara(1);
        }
    }

    private void IrPara(int meses)
    {
        var destino = new DateTime(Ano, Mes, 1).AddMonths(meses);
        Ir(destino.Year, destino.Month);
    }

    private void Ir(int ano, int mes)
    {
        Ano = ano;
        Mes = mes;

        OnPropertyChanged(nameof(NoMesAtual));
        CarregarCommand.Execute(null);
    }

    /// <summary>Abre o seletor já no ano do mês que está na tela.</summary>
    [RelayCommand]
    private void AbrirSeletor()
    {
        AnoDoSeletor = Ano;
        MontarSeletor();
        SeletorAberto = true;
    }

    [RelayCommand]
    private void AnoAnterior()
    {
        AnoDoSeletor--;
        MontarSeletor();
    }

    [RelayCommand]
    private void AnoSeguinte()
    {
        if (PodeAvancarAno)
        {
            AnoDoSeletor++;
            MontarSeletor();
        }
    }

    /// <summary>Escolher um mês fecha o seletor e carrega: é um gesto só.</summary>
    [RelayCommand]
    private void EscolherMes(OpcaoDeMes? opcao)
    {
        if (opcao is not { Disponivel: true })
        {
            return;
        }

        SeletorAberto = false;
        Ir(AnoDoSeletor, opcao.Numero);
    }

    /// <summary>Volta ao mês corrente de qualquer lugar do histórico.</summary>
    [RelayCommand]
    private void IrParaHoje()
    {
        SeletorAberto = false;
        Ir(DateTime.Today.Year, DateTime.Today.Month);
    }

    /// <summary>Os doze meses do ano escolhido, com o futuro desligado.</summary>
    private void MontarSeletor()
    {
        MesesDoSeletor.Clear();

        for (var mes = 1; mes <= 12; mes++)
        {
            // Mês que ainda não chegou não tem caixa: aparece, para a grade não mudar
            // de forma no meio do ano, mas não recebe clique.
            var disponivel = new DateTime(AnoDoSeletor, mes, 1)
                             <= new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            MesesDoSeletor.Add(new OpcaoDeMes(
                mes,
                disponivel,
                escolhido: AnoDoSeletor == Ano && mes == Mes));
        }

        OnPropertyChanged(nameof(PodeAvancarAno));
    }

    /// <summary>Busca a cada tecla: a lista já está na memória, não há ida ao banco.</summary>
    partial void OnTermoBuscaChanged(string value)
    {
        AplicarBusca();
        OnPropertyChanged(nameof(TemBusca));
    }

    [RelayCommand]
    private void LimparBusca() => TermoBusca = string.Empty;

    private void AplicarBusca()
    {
        var termo = TermoBusca.Trim().ToLowerInvariant();

        var filtradas = termo.Length == 0
            ? _todasAsCobrancas
            : [.. _todasAsCobrancas.Where(c => c.Busca.Contains(termo, StringComparison.Ordinal))];

        Cobrancas.Clear();
        foreach (var cobranca in filtradas)
        {
            Cobrancas.Add(cobranca);
        }

        OnPropertyChanged(nameof(SemCobrancas));
        OnPropertyChanged(nameof(MensagemSemCobrancas));
    }

    /// <summary>Leva à ficha do cliente, para o que esta tela não mostra.</summary>
    [RelayCommand]
    private void AbrirCliente(int clienteId) => _aoAbrirCliente?.Invoke(clienteId);

    /// <summary>Lança o pagamento sem sair da cobrança.</summary>
    /// <remarks>
    /// É o fecho da tela: quem acabou de ligar cobrando registra o que entrou ali mesmo,
    /// e a linha some da lista na recarga. Abrir a ficha para isso seria devolver a
    /// pessoa ao caminho que a tela existe para encurtar.
    /// </remarks>
    [RelayCommand]
    private void Receber(CobrancaViewModel? cobranca)
    {
        if (cobranca is null)
        {
            return;
        }

        Formulario = new PagamentoFormularioViewModel(
            cobranca.ContratacaoId,
            cobranca.Dados.EmAberto,
            $"{cobranca.Nome} · {cobranca.ContratadoEm}",
            cobranca.Contratado,
            cobranca.Pago,
            cobranca.EmAberto,
            aoCancelar: () => Formulario = null,
            aoSalvar: () =>
            {
                Formulario = null;
                CarregarCommand.Execute(null);
            });
    }

    /// <summary>"Novembro de 2026", com a primeira letra maiúscula.</summary>
    private static string MesPorExtenso(int ano, int mes)
    {
        var nome = new DateTime(ano, mes, 1).ToString("MMMM 'de' yyyy", Brasil);

        return char.ToUpper(nome[0], Brasil) + nome[1..];
    }
}

/// <summary>Um mês na grade do seletor.</summary>
public class OpcaoDeMes
{
    private static readonly CultureInfo Brasil = new("pt-BR");

    public OpcaoDeMes(int numero, bool disponivel, bool escolhido)
    {
        Numero = numero;
        Disponivel = disponivel;
        Escolhido = escolhido;

        // "jan.", "fev." no pt-BR: o ponto some, que numa grade de três colunas só
        // ocupa espaço e não diz nada.
        var nome = Brasil.DateTimeFormat.AbbreviatedMonthNames[numero - 1].TrimEnd('.');
        Nome = char.ToUpper(nome[0], Brasil) + nome[1..];
    }

    public int Numero { get; }
    public string Nome { get; }
    public bool Disponivel { get; }

    /// <summary>É o mês que está carregado na tela.</summary>
    public bool Escolhido { get; }
}

/// <summary>Quanto entrou por uma forma de pagamento, com a barra proporcional.</summary>
public class FatiaDaFormaViewModel
{
    public FatiaDaFormaViewModel(FormaPagamento forma, decimal valor, decimal maior)
    {
        Nome = Rotulos.Forma(forma, 1);
        Valor = Formato.Dinheiro(valor);

        // Largura em proporção da maior fatia, numa faixa de 160 pixels.
        Largura = maior > 0 ? (double)(valor / maior) * 160 : 0;
    }

    public string Nome { get; }
    public string Valor { get; }
    public double Largura { get; }
}
