using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Documentos;
using GestorRecursos.Core.Models;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>O recorte da aba Serviços.</summary>
public enum FiltroServico { Ativos, Encerrados, Todos }

/// <summary>O recorte da aba Financeiro.</summary>
public enum FiltroLancamento { EmAberto, Quitadas, Todas }

/// <summary>As abas da ficha.</summary>
public enum AbaFicha
{
    Dados,
    Veiculos,
    Servicos,
    Financeiro,
    Documentos,
    Historico
}

/// <summary>Uma aba, com o contador que aparece ao lado do nome.</summary>
public partial class AbaFichaViewModel(string titulo, AbaFicha aba, bool pronta) : ObservableObject
{
    public string Titulo { get; } = titulo;
    public AbaFicha Aba { get; } = aba;

    /// <summary>Aba que ainda não foi construída: aparece, mas não abre.</summary>
    public bool Pronta { get; } = pronta;

    [ObservableProperty]
    public partial bool Escolhida { get; set; }

    /// <summary>Quantos itens a aba tem. Zero esconde o selo.</summary>
    [ObservableProperty]
    public partial int Contador { get; set; }

    public bool TemContador => Contador > 0;

    partial void OnContadorChanged(int valor) => OnPropertyChanged(nameof(TemContador));
}

/// <summary>Um cliente na lista reduzida, ao lado da ficha.</summary>
public partial class LinhaReduzidaViewModel(ResumoCliente dados) : ObservableObject
{
    public int Id { get; } = dados.Id;
    public string Nome { get; } = dados.Nome;
    public string Detalhe { get; } =
        $"{Formato.Cpf(dados.Cpf)}{(string.IsNullOrWhiteSpace(dados.Cidade) ? "" : $" · {dados.Cidade}/{dados.Estado}")}";

    public string? PrimeiraPlaca { get; } = dados.Placas?.Split(',').FirstOrDefault()?.Trim();
    public bool TemPlaca => !string.IsNullOrEmpty(PrimeiraPlaca);

    public bool DevendoAlgo { get; } = dados.EmAberto > 0;
    public string Situacao { get; } = dados.EmAberto > 0 ? Formato.Dinheiro(dados.EmAberto) : "em dia";

    [ObservableProperty]
    public partial bool Aberta { get; set; }
}

/// <summary>A ficha do cliente: cabeçalho, abas e a lista reduzida ao lado.</summary>
/// <remarks>
/// A lista fica junto de propósito (maquete E2): o escritório trabalha em sequência, um
/// cliente atrás do outro, e voltar para a listagem a cada troca custaria dois cliques e
/// a perda do lugar na lista.
/// </remarks>
public partial class FichaClienteViewModel : ViewModelBase
{
    private readonly FichaClienteDAO _fichas = new();
    private readonly ClienteDAO _clientes = new();
    private readonly ContratacaoDAO _contratacoes = new();
    private readonly PagamentoDAO _pagamentos = new();
    private readonly EscritorioDAO _escritorios = new();
    private readonly HistoricoDAO _historicos = new();
    private readonly Action? _aoVoltar;
    private readonly Action? _aoNovoCliente;
    private readonly Action<GrupoCadastro>? _aoEditar;

    /// <summary>Abrir o assistente de servico assim que a ficha terminar de carregar.</summary>
    private bool _abrirNovoServico;

    /// <summary>Pergunta aberta por cima da ficha. Nula quando não há nenhuma.</summary>
    [ObservableProperty]
    public partial ConfirmacaoViewModel? Confirmacao { get; set; }

    /// <summary>Quantos vizinhos já vieram do banco; a lista cresce conforme rola.</summary>
    private const int PorLote = 25;
    private bool _carregandoVizinhos;
    private int _paginaVizinhos;
    private int _totalVizinhos;

    public ObservableCollection<AbaFichaViewModel> Abas { get; } =
    [
        new("Dados", AbaFicha.Dados, pronta: true),
        new("Veículos e multas", AbaFicha.Veiculos, pronta: true),
        new("Serviços", AbaFicha.Servicos, pronta: true),
        new("Financeiro", AbaFicha.Financeiro, pronta: true),
        new("Documentos", AbaFicha.Documentos, pronta: true),
        new("Histórico", AbaFicha.Historico, pronta: true)
    ];

    /// <summary>Os outros clientes, para trocar sem sair da ficha.</summary>
    public ObservableCollection<LinhaReduzidaViewModel> Vizinhos { get; } = [];

    public ObservableCollection<CampoFicha> DadosPessoais { get; } = [];
    public ObservableCollection<CampoFicha> ContatoEndereco { get; } = [];
    public ObservableCollection<CampoFicha> DadosCnh { get; } = [];
    public ObservableCollection<VeiculoDaFichaViewModel> Veiculos { get; } = [];
    public ObservableCollection<ProcessoDaFichaViewModel> Processos { get; } = [];

    [ObservableProperty]
    public partial int ClienteId { get; set; }

    [ObservableProperty]
    public partial string Nome { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Identificacao { get; set; } = string.Empty;

    /// <summary>Anotacao livre do cliente, mostrada na aba Dados.</summary>
    [ObservableProperty]
    public partial string? Observacoes { get; set; }

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? Erro { get; set; }

    [ObservableProperty]
    public partial AbaFicha AbaAtual { get; set; } = AbaFicha.Dados;

    [ObservableProperty]
    public partial string FiltroDaLista { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ContagemDaLista { get; set; } = string.Empty;

    // Resumo financeiro do cabeçalho da aba Dados
    [ObservableProperty]
    public partial string EmAberto { get; set; } = "—";

    [ObservableProperty]
    public partial string JaPago { get; set; } = "—";

    [ObservableProperty]
    public partial string TotalContratado { get; set; } = "—";

    [ObservableProperty]
    public partial int PercentualQuitado { get; set; }

    [ObservableProperty]
    public partial string NotaDoFinanceiro { get; set; } = string.Empty;

    /// <summary>Sem contratação não há o que medir: a barra some em vez de aparecer cheia.</summary>
    [ObservableProperty]
    public partial bool TemContratacao { get; set; }


    /// <summary>Selos de alerta do cabeçalho: prazo, fase e valor em aberto.</summary>
    public ObservableCollection<SeloViewModel> Selos { get; } = [];

    public bool TemVeiculos => Veiculos.Count > 0;
    public bool TemProcessos => Processos.Count > 0;
    public bool TemObservacoes => !string.IsNullOrWhiteSpace(Observacoes);

    /// <summary>Atalho para o Financeiro só faz sentido quando a aba abrir.</summary>
    public bool FinanceiroPronto => AchaAba(AbaFicha.Financeiro).Pronta;

    public bool AbaDados => AbaAtual == AbaFicha.Dados;
    public bool AbaVeiculos => AbaAtual == AbaFicha.Veiculos;
    public bool AbaServicos => AbaAtual == AbaFicha.Servicos;
    public bool AbaFinanceiro => AbaAtual == AbaFicha.Financeiro;
    public bool AbaDocumentos => AbaAtual == AbaFicha.Documentos;
    public bool AbaHistorico => AbaAtual == AbaFicha.Historico;

    /// <summary>Os cartões da aba Veículos e multas.</summary>
    public ObservableCollection<VeiculoComMultasViewModel> VeiculosEMultas { get; } = [];

    /// <summary>Os cartões da aba Serviços: recurso com as três fases, serviço direto com o roteiro.</summary>
    public ObservableCollection<ServicoDaFichaViewModel> Servicos { get; } = [];

    /// <summary>Os cartões da aba Financeiro: uma contratação por cartão, com os pagamentos.</summary>
    public ObservableCollection<LancamentoDaFichaViewModel> Lancamentos { get; } = [];

    public bool SemLancamentos => !CarregandoFinanceiro && Lancamentos.Count == 0;

    /// <summary>A linha do tempo da aba Histórico, do mais recente para o mais antigo.</summary>
    public ObservableCollection<EventoDaFichaViewModel> Historico { get; } = [];

    public bool SemHistorico => !CarregandoHistorico && _historicoCarregado && Historico.Count == 0;

    // As listas completas ficam de lado; o que a tela mostra é o recorte do filtro.
    // Filtrar em memória evita uma ida ao banco a cada clique em sub-aba.
    private readonly List<ServicoDaFichaViewModel> _todosServicos = [];
    private readonly List<LancamentoDaFichaViewModel> _todosLancamentos = [];

    [ObservableProperty] public partial FiltroServico FiltroDeServico { get; set; } = FiltroServico.Ativos;
    [ObservableProperty] public partial FiltroLancamento FiltroDeLancamento { get; set; } = FiltroLancamento.Todas;

    /// <summary>Contratação escolhida no financeiro, para ver só os serviços dela.</summary>
    [ObservableProperty] public partial int? ContratacaoEmFoco { get; set; }
    [ObservableProperty] public partial string? TituloEmFoco { get; set; }

    public bool TemFocoDeContratacao => ContratacaoEmFoco is not null;

    public bool FiltroEmAtivos => FiltroDeServico == FiltroServico.Ativos;
    public bool FiltroEmEncerrados => FiltroDeServico == FiltroServico.Encerrados;
    public bool FiltroEmTodos => FiltroDeServico == FiltroServico.Todos;

    public bool LancamentosEmAberto => FiltroDeLancamento == FiltroLancamento.EmAberto;
    public bool LancamentosQuitados => FiltroDeLancamento == FiltroLancamento.Quitadas;
    public bool LancamentosTodos => FiltroDeLancamento == FiltroLancamento.Todas;

    /// <summary>Formulário aberto por cima da ficha. Nulo quando não há nenhum.</summary>
    /// <remarks>
    /// Mora na ficha, e não na casca da seção, porque quem abre veículo e multa é sempre
    /// a ficha: o formulário nasce de um cartão e volta para ele.
    /// </remarks>
    [ObservableProperty]
    public partial ViewModelBase? Formulario { get; set; }

    [ObservableProperty]
    public partial bool CarregandoVeiculos { get; set; }

    [ObservableProperty]
    public partial bool CarregandoServicos { get; set; }

    [ObservableProperty]
    public partial bool CarregandoFinanceiro { get; set; }

    /// <summary>A procuração aberta por cima da ficha. Nula quando não há nenhuma.</summary>
    [ObservableProperty]
    public partial ProcuracaoViewModel? ProcuracaoAberta { get; set; }

    [ObservableProperty]
    public partial bool CarregandoHistorico { get; set; }

    /// <summary>Evita ir ao banco de novo a cada vez que a aba é reaberta.</summary>
    private bool _veiculosCarregados;
    private bool _servicosCarregados;
    private bool _financeiroCarregado;
    private bool _historicoCarregado;

    /// <summary>O cliente inteiro, guardado para a aba Documentos montar a procuração.</summary>
    /// <remarks>
    /// A ficha exibe campos formatados; o documento precisa dos valores como estão no
    /// banco. Reler o cliente só para gerar o papel seria uma ida à nuvem já feita.
    /// </remarks>
    private Cliente? _cliente;

    /// <summary>A outorgada. Uma linha só no banco, e a mesma para todos os clientes.</summary>
    private Escritorio? _escritorio;

    public bool SemVeiculos => !CarregandoVeiculos && _veiculosCarregados && VeiculosEMultas.Count == 0;
    public bool SemServicos => !CarregandoServicos && _servicosCarregados && Servicos.Count == 0;

    /// <summary>Quantos recursos já não esperam nada: entram na contagem do rodapé.</summary>
    public int ServicosEncerrados => _todosServicos.Count(r => r.Encerrado);
    public bool TemEncerrados => ServicosEncerrados > 0;

    public string ResumoEncerrados => ServicosEncerrados == 1
        ? "1 serviço encerrado"
        : $"{ServicosEncerrados} serviços encerrados";

    public FichaClienteViewModel()
    {
        // Construtor sem argumentos só para o preview do Rider.
    }

    public FichaClienteViewModel(
        int clienteId,
        Action? aoVoltar = null,
        Action? aoNovoCliente = null,
        Action<GrupoCadastro>? aoEditar = null,
        bool abrirNovoServico = false)
    {
        ClienteId = clienteId;
        _aoVoltar = aoVoltar;
        _aoNovoCliente = aoNovoCliente;
        _aoEditar = aoEditar;
        _abrirNovoServico = abrirNovoServico;

        if (!Design.IsDesignMode)
        {
            CarregarCommand.Execute(null);
        }
    }

    [RelayCommand]
    private async Task Carregar()
    {
        Carregando = true;
        Erro = null;

        // As abas pesadas guardam o que já buscaram; recarregar a ficha invalida isso.
        // Sem limpar aqui, abrir outro cliente mostrava os veículos e os processos
        // do anterior, e um recurso recém-criado não aparecia na aba já aberta.
        EsquecerAbas();

        try
        {
            var ficha = await _fichas.BuscarAsync(ClienteId);
            if (ficha is null)
            {
                Erro = "Cliente não encontrado. O registro pode ter sido excluído em outra tela.";
                return;
            }

            Preencher(ficha);
            await CarregarVizinhos();

            // Quem veio de "Salvar e criar serviço" já pediu o assistente: abre uma vez,
            // com a ficha montada, e nunca mais nas recargas seguintes.
            if (_abrirNovoServico)
            {
                _abrirNovoServico = false;
                NovoServico();
            }
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível abrir a ficha: {excecao.Message}";
        }
        finally
        {
            Carregando = false;
        }
    }

    [RelayCommand]
    private void Voltar() => _aoVoltar?.Invoke();


    [RelayCommand]
    private async Task TrocarAba(AbaFicha aba)
    {
        MarcarAba(aba);

        await CarregarAbaAtual();
    }

    /// <summary>Enche a aba aberta, se ela ainda não tiver o que mostrar.</summary>
    /// <remarks>
    /// As duas abas pesadas carregam sob demanda e guardam o resultado. Quem precisa
    /// delas cheias — trocar de aba ou recarregar depois de gravar — passa por aqui,
    /// em vez de cada chamador lembrar qual aba busca o quê.
    /// </remarks>
    private async Task CarregarAbaAtual()
    {
        if (AbaAtual == AbaFicha.Veiculos && !_veiculosCarregados)
        {
            await CarregarVeiculosEMultas();
        }
        else if (AbaAtual == AbaFicha.Servicos && !_servicosCarregados)
        {
            await CarregarServicos();
        }
        else if (AbaAtual == AbaFicha.Financeiro && !_financeiroCarregado)
        {
            await CarregarFinanceiro();
        }
        else if (AbaAtual == AbaFicha.Historico && !_historicoCarregado)
        {
            await CarregarHistorico();
        }
    }

    /// <summary>Faz as abas pesadas esquecerem o que buscaram.</summary>
    private void EsquecerAbas()
    {
        _veiculosCarregados = false;
        VeiculosEMultas.Clear();

        _servicosCarregados = false;
        _todosServicos.Clear();
        Servicos.Clear();

        _financeiroCarregado = false;
        _todosLancamentos.Clear();
        Lancamentos.Clear();

        _historicoCarregado = false;
        Historico.Clear();

        // A procuração é de um cliente só: trocar de ficha fecha a que estiver aberta.
        ProcuracaoAberta = null;
    }

    /// <summary>Só o destaque da aba. Separado de <see cref="TrocarAba"/> para quem
    /// precisa reaplicar a marcação sem disparar busca no banco.</summary>
    partial void OnObservacoesChanged(string? valor) => OnPropertyChanged(nameof(TemObservacoes));

    private void MarcarAba(AbaFicha aba)
    {
        AbaAtual = aba;

        foreach (var item in Abas)
        {
            item.Escolhida = item.Aba == aba;
        }
    }

    /// <summary>Busca os veículos e as multas na primeira vez que a aba é aberta.</summary>
    /// <remarks>
    /// Fora da carga da ficha porque são duas consultas a mais, e a ficha abre em Dados:
    /// quem só quer o telefone do cliente não paga por elas.
    /// </remarks>
    private async Task CarregarVeiculosEMultas()
    {
        CarregandoVeiculos = true;

        try
        {
            var veiculos = await _fichas.ListarVeiculosComMultasAsync(ClienteId);

            VeiculosEMultas.Clear();
            foreach (var veiculo in veiculos)
            {
                VeiculosEMultas.Add(new VeiculoComMultasViewModel(veiculo));
            }

            _veiculosCarregados = true;
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível carregar os veículos: {excecao.Message}";
        }
        finally
        {
            CarregandoVeiculos = false;
            OnPropertyChanged(nameof(SemVeiculos));
        }
    }

    /// <summary>Busca os recursos com as três fases, na primeira vez que a aba é aberta.</summary>
    private async Task CarregarServicos()
    {
        CarregandoServicos = true;

        try
        {
            var servicos = await _fichas.ListarServicosAsync(ClienteId);

            _todosServicos.Clear();
            foreach (var servico in servicos)
            {
                _todosServicos.Add(new ServicoDaFichaViewModel(servico));
            }

            AplicarFiltroDeServico();
            _servicosCarregados = true;
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível carregar os processos: {excecao.Message}";
        }
        finally
        {
            CarregandoServicos = false;
            OnPropertyChanged(nameof(SemServicos));
            OnPropertyChanged(nameof(ServicosEncerrados));
            OnPropertyChanged(nameof(TemEncerrados));
            OnPropertyChanged(nameof(ResumoEncerrados));
        }
    }

    /// <summary>Registra o protocolo de uma fase: é o que desliga o alarme do prazo.</summary>
    [RelayCommand]
    private void RegistrarProtocolo(FaseViewModel? fase)
    {
        if (fase?.Servico is not { } servico)
        {
            return;
        }

        Formulario = new AndamentoFormularioViewModel(
            AcaoAndamento.Protocolo, servico, fase,
            aoCancelar: () => Formulario = null,
            aoSalvar: Recarregar);
    }

    /// <summary>Registra o julgamento, ou anula a fase. Os dois abrem a seguinte sozinhos.</summary>
    /// <remarks>
    /// Serve aos dois botões: o formulário se ajusta pela fase que recebe. Protocolada,
    /// pergunta o que o órgão decidiu; não protocolada, só resta ter sido anulada.
    /// </remarks>
    [RelayCommand]
    private void RegistrarResultado(FaseViewModel? fase)
    {
        if (fase?.Servico is not { } servico)
        {
            return;
        }

        Formulario = new AndamentoFormularioViewModel(
            AcaoAndamento.Resultado, servico, fase,
            aoCancelar: () => Formulario = null,
            aoSalvar: Recarregar);
    }

    /// <summary>Marca ou desmarca uma etapa do roteiro de um serviço direto.</summary>
    /// <remarks>
    /// Grava na hora e não recarrega a ficha: marcar etapa é um gesto solto, repetido,
    /// que não mexe em dinheiro nem em prazo. Recarregar a cada clique faria a lista
    /// piscar e tiraria o lugar de onde ela estava lendo.
    /// </remarks>
    [RelayCommand]
    private async Task MarcarEtapa(EtapaViewModel? etapa)
    {
        if (etapa?.PodeMarcar != true)
        {
            return;
        }

        var alvo = !etapa.Concluida;

        try
        {
            await new EtapaDAO().MarcarAsync(etapa.Id, alvo);

            // Marcar grava hoje no banco; a linha e o contador do cartão acompanham sem
            // recarregar a ficha inteira, que faria a lista piscar a cada clique.
            etapa.AplicarMarca(alvo, alvo ? DateTime.Today : null);
            etapa.Servico?.Recontar();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível marcar a etapa: {excecao.Message}";
        }
    }

    /// <summary>Enche a aba Financeiro: as contratações do cliente e o que entrou em cada uma.</summary>
    /// <remarks>
    /// Três consultas, e não uma por contratação: pagamentos e despesas vêm todos de uma
    /// vez e são agrupados aqui. O nome dos serviços sai da mesma consulta que alimenta a
    /// aba Serviços, porque a contratação sozinha não sabe o que foi contratado.
    /// </remarks>
    private async Task CarregarFinanceiro()
    {
        CarregandoFinanceiro = true;

        try
        {
            var contratacoes = await _contratacoes.ListarPorClienteAsync(ClienteId);
            var pagamentos = await _pagamentos.ListarPorClienteAsync(ClienteId);
            var despesas = await _pagamentos.ListarDespesasPorClienteAsync(ClienteId);
            var servicos = await _fichas.ListarServicosAsync(ClienteId);

            _todosLancamentos.Clear();
            foreach (var contratacao in contratacoes)
            {
                _todosLancamentos.Add(new LancamentoDaFichaViewModel(
                    contratacao,
                    servicos.Where(s => s.ContratacaoId == contratacao.Id).ToList(),
                    pagamentos.Where(p => p.ContratacaoId == contratacao.Id),
                    despesas.Where(d => d.ContratacaoId == contratacao.Id)));
            }

            AplicarFiltroDeLancamento();
            _financeiroCarregado = true;
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível carregar o financeiro: {excecao.Message}";
        }
        finally
        {
            CarregandoFinanceiro = false;
            OnPropertyChanged(nameof(SemLancamentos));
        }
    }

    /// <summary>Enche a aba Histórico: a linha do tempo do cliente.</summary>
    /// <remarks>
    /// Os eventos são deduzidos das datas que as tabelas já guardam, numa consulta só.
    /// Por isso a aba mostra o que foi feito, e não quem fez nem qual campo mudou: para
    /// isso faltam a tabela de eventos e o login, que ainda não existem.
    /// </remarks>
    private async Task CarregarHistorico()
    {
        CarregandoHistorico = true;

        try
        {
            var eventos = await _historicos.ListarAsync(ClienteId);

            Historico.Clear();
            foreach (var evento in eventos)
            {
                Historico.Add(new EventoDaFichaViewModel(evento));
            }

            _historicoCarregado = true;
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível carregar o histórico: {excecao.Message}";
        }
        finally
        {
            CarregandoHistorico = false;
            OnPropertyChanged(nameof(SemHistorico));
        }
    }

    /// <summary>Abre a procuração por cima da ficha, montada na hora.</summary>
    /// <remarks>
    /// Nada é guardado: o PDF sai do cadastro de hoje, e uma cópia gravada viraria uma
    /// segunda versão do mesmo papel. Gerar de novo custa menos que manter as duas.
    /// </remarks>
    [RelayCommand]
    private async Task AbrirProcuracao()
    {
        if (_cliente is null)
        {
            return;
        }

        try
        {
            // A outorgada é a mesma para todos os clientes: busca uma vez por sessão.
            _escritorio ??= await _escritorios.BuscarAsync();

            var procuracao = new ProcuracaoViewModel(
                _cliente,
                _escritorio,
                aoFechar: () => ProcuracaoAberta = null,
                aoCorrigir: parte =>
                {
                    ProcuracaoAberta = null;
                    Editar(TraduzirGrupo(parte));
                });

            ProcuracaoAberta = procuracao;
            await procuracao.PrepararAsync();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível abrir a procuração: {excecao.Message}";
        }
    }

    // A Core diz onde o dado é digitado; a tela sabe o nome do grupo no cadastro.
    // Dado do escritório não se edita por aqui: cai nos dados pessoais, que é onde ela
    // começa a procurar, e a lista de faltas já nomeia o que é.
    private static GrupoCadastro TraduzirGrupo(ParteDoCadastro parte) => parte switch
    {
        ParteDoCadastro.ContatoEndereco => GrupoCadastro.ContatoEndereco,
        ParteDoCadastro.Cnh => GrupoCadastro.Cnh,
        _ => GrupoCadastro.DadosPessoais
    };

    /// <summary>Troca o recorte da aba Serviços.</summary>
    [RelayCommand]
    private void FiltrarServicos(FiltroServico filtro)
    {
        FiltroDeServico = filtro;
        AplicarFiltroDeServico();
    }

    /// <summary>Troca o recorte da aba Financeiro.</summary>
    [RelayCommand]
    private void FiltrarLancamentos(FiltroLancamento filtro)
    {
        FiltroDeLancamento = filtro;
        AplicarFiltroDeLancamento();
    }

    /// <summary>Abre a aba Serviços mostrando só os serviços daquela contratação.</summary>
    /// <remarks>
    /// Com muitas contratações, saber que uma delas custou um valor não diz qual recurso
    /// ela cobre. Este caminho responde isso sem obrigar a procurar na lista inteira.
    /// </remarks>
    [RelayCommand]
    private async Task VerServicosDaContratacao(LancamentoDaFichaViewModel? lancamento)
    {
        if (lancamento is null)
        {
            return;
        }

        ContratacaoEmFoco = lancamento.Id;
        TituloEmFoco = lancamento.Titulo;
        FiltroDeServico = FiltroServico.Todos;

        MarcarAba(AbaFicha.Servicos);
        await CarregarAbaAtual();
        AplicarFiltroDeServico();
    }

    /// <summary>Volta a mostrar os serviços de todas as contratações.</summary>
    [RelayCommand]
    private void LimparFoco()
    {
        ContratacaoEmFoco = null;
        TituloEmFoco = null;
        AplicarFiltroDeServico();
    }

    private void AplicarFiltroDeServico()
    {
        Servicos.Clear();
        foreach (var servico in _todosServicos.Where(Cabe))
        {
            Servicos.Add(servico);
        }

        OnPropertyChanged(nameof(SemServicos));
        OnPropertyChanged(nameof(FiltroEmAtivos));
        OnPropertyChanged(nameof(FiltroEmEncerrados));
        OnPropertyChanged(nameof(FiltroEmTodos));
        OnPropertyChanged(nameof(ServicosEncerrados));
        OnPropertyChanged(nameof(TemEncerrados));
        OnPropertyChanged(nameof(ResumoEncerrados));
        OnPropertyChanged(nameof(TemFocoDeContratacao));
        return;

        bool Cabe(ServicoDaFichaViewModel servico) =>
            (ContratacaoEmFoco is not { } contratacao || servico.ContratacaoId == contratacao)
            && FiltroDeServico switch
            {
                FiltroServico.Ativos => !servico.Encerrado,
                FiltroServico.Encerrados => servico.Encerrado,
                _ => true
            };
    }

    private void AplicarFiltroDeLancamento()
    {
        Lancamentos.Clear();
        foreach (var lancamento in _todosLancamentos.Where(Cabe))
        {
            Lancamentos.Add(lancamento);
        }

        OnPropertyChanged(nameof(SemLancamentos));
        OnPropertyChanged(nameof(LancamentosEmAberto));
        OnPropertyChanged(nameof(LancamentosQuitados));
        OnPropertyChanged(nameof(LancamentosTodos));
        return;

        bool Cabe(LancamentoDaFichaViewModel lancamento) => FiltroDeLancamento switch
        {
            FiltroLancamento.EmAberto => lancamento.Devendo,
            FiltroLancamento.Quitadas => lancamento.Quitado,
            _ => true
        };
    }

    /// <summary>Lança uma entrada de dinheiro na contratação escolhida.</summary>
    [RelayCommand]
    private void LancarPagamento(LancamentoDaFichaViewModel? lancamento)
    {
        if (lancamento is null)
        {
            return;
        }

        Formulario = new PagamentoFormularioViewModel(
            lancamento,
            aoCancelar: () => Formulario = null,
            aoSalvar: Recarregar);
    }

    /// <summary>Pergunta antes de apagar um pagamento lançado errado.</summary>
    [RelayCommand]
    private void PedirExclusaoPagamento(PagamentoDaFichaViewModel? pagamento)
    {
        if (pagamento is null)
        {
            return;
        }

        Confirmacao = new ConfirmacaoViewModel(
            titulo: "Apagar este pagamento?",
            texto: $"O lançamento de {pagamento.Valor}, em {pagamento.Quando}, será removido "
                 + "e o valor volta a constar em aberto. Use apenas para corrigir um "
                 + "lançamento equivocado.",
            botao: "Apagar pagamento",
            aoConfirmar: () => ApagarPagamento(pagamento.Id),
            aoCancelar: () => Confirmacao = null);
    }

    private async void ApagarPagamento(int pagamentoId)
    {
        try
        {
            await _pagamentos.ExcluirAsync(pagamentoId);
            Confirmacao = null;
            Recarregar();
        }
        catch (Exception excecao)
        {
            Confirmacao = null;
            Erro = $"Não foi possível apagar o pagamento: {excecao.Message}";
        }
    }

    /// <summary>Abre as datas da etapa: o prazo dela e quando foi cumprida.</summary>
    [RelayCommand]
    private void EditarEtapa(EtapaViewModel? etapa)
    {
        if (etapa is null)
        {
            return;
        }

        Formulario = new EtapaFormularioViewModel(
            etapa,
            aoCancelar: () => Formulario = null,
            aoSalvar: Recarregar);
    }

    /// <summary>Corrige o serviço contratado: a variante e o valor.</summary>
    [RelayCommand]
    private void EditarServico(ServicoDaFichaViewModel? servico)
    {
        if (servico is null)
        {
            return;
        }

        Formulario = new ServicoFormularioViewModel(
            servico,
            aoCancelar: () => Formulario = null,
            aoSalvar: Recarregar);
    }

    /// <summary>Pergunta antes de encerrar o serviço, e explica o que fica e o que para.</summary>
    [RelayCommand]
    private void PedirCancelamento(ServicoDaFichaViewModel? servico)
    {
        if (servico is null)
        {
            return;
        }

        Confirmacao = new ConfirmacaoViewModel(
            titulo: "Encerrar este serviço?",
            texto: "As fases e etapas pendentes são encerradas e o serviço sai de "
                 + "andamento. A contratação é mantida, com o valor cobrado, e o que já "
                 + "foi concluído permanece registrado.",
            botao: "Encerrar serviço",
            aoConfirmar: () => Cancelar(servico.ItemId),
            aoCancelar: () => Confirmacao = null);
    }

    private async void Cancelar(int itemId)
    {
        try
        {
            await new InstanciaDAO().CancelarRecursoAsync(itemId);
            Confirmacao = null;
            Recarregar();
        }
        catch (Exception excecao)
        {
            Confirmacao = null;
            Erro = $"Não foi possível encerrar o recurso: {excecao.Message}";
        }
    }

    /// <summary>Fecha o formulário, recarrega a ficha e enche de novo a aba aberta.</summary>
    /// <remarks>
    /// Serve a toda gravação da ficha — veículo, multa, recurso, andamento. Tudo que se
    /// grava aqui aparece em mais de um lugar: a multa muda a aba de veículos, o selo de
    /// prazo do cabeçalho e o cartão do processo. Recarregar por inteiro é mais barato
    /// que cada comando lembrar o que mais precisa mudar — e foi o esquecimento disso
    /// que fez um recurso recém-criado não aparecer na aba Processos já aberta.
    /// </remarks>
    private async void Recarregar()
    {
        Formulario = null;

        // Carregar() já faz as abas esquecerem o que buscaram.
        await Carregar();
        await CarregarAbaAtual();
    }

    /// <summary>Abre outro cliente sem sair da ficha.</summary>
    [RelayCommand]
    private async Task AbrirVizinho(int id)
    {
        if (id == ClienteId)
        {
            return;
        }

        ClienteId = id;
        await TrocarAba(AbaFicha.Dados);
        await Carregar();
    }

    [RelayCommand]
    private Task FiltrarLista() => CarregarVizinhos();

    private void Preencher(FichaCliente ficha)
    {
        var cliente = ficha.Cliente;
        _cliente = cliente;

        Nome = cliente.Nome;
        Identificacao = MontarIdentificacao(cliente);
        Observacoes = cliente.Observacoes;

        DadosPessoais.Clear();
        DadosPessoais.Add(new CampoFicha("Nome", cliente.Nome));
        DadosPessoais.Add(new CampoFicha("CPF", Formato.Cpf(cliente.Cpf)));
        DadosPessoais.Add(new CampoFicha("RG", JuntarRg(cliente.Rg, cliente.RgOrgao)));
        DadosPessoais.Add(new CampoFicha("Nacionalidade", cliente.Nacionalidade));
        DadosPessoais.Add(new CampoFicha("Estado civil", cliente.EstadoCivil));
        DadosPessoais.Add(new CampoFicha("Profissão", cliente.Profissao));

        ContatoEndereco.Clear();
        ContatoEndereco.Add(new CampoFicha("WhatsApp", Formato.Telefone(cliente.Whatsapp)));
        ContatoEndereco.Add(new CampoFicha("E-mail", cliente.Email));
        ContatoEndereco.Add(new CampoFicha("Endereço", MontarLogradouro(cliente.Endereco)));
        ContatoEndereco.Add(new CampoFicha("Bairro", cliente.Endereco?.Bairro));
        ContatoEndereco.Add(new CampoFicha("Cidade", MontarCidade(cliente.Endereco)));
        ContatoEndereco.Add(new CampoFicha("CEP", Formato.Cep(cliente.Endereco?.Cep)));

        // O "Registro" saiu da ficha junto com o campo do formulário: era o mesmo número
        // do de cima. A coluna continua no banco, só não se mostra mais.
        DadosCnh.Clear();
        DadosCnh.Add(new CampoFicha("Nº Registro", cliente.CnhNumero));
        DadosCnh.Add(new CampoFicha("Categoria", cliente.CnhCategoria));
        DadosCnh.Add(new CampoFicha("Validade", Formato.Data(cliente.CnhValidade)));

        Veiculos.Clear();
        foreach (var veiculo in ficha.Veiculos)
        {
            Veiculos.Add(new VeiculoDaFichaViewModel(veiculo));
        }

        Processos.Clear();
        foreach (var processo in ficha.Processos)
        {
            Processos.Add(new ProcessoDaFichaViewModel(processo));
        }

        PreencherFinanceiro(ficha.Financeiro);
        PreencherSelos(ficha);

        AchaAba(AbaFicha.Veiculos).Contador = ficha.Veiculos.Count;
        AchaAba(AbaFicha.Servicos).Contador = ficha.Processos.Count;
        MarcarAba(AbaAtual);

        OnPropertyChanged(nameof(TemVeiculos));
        OnPropertyChanged(nameof(TemProcessos));
    }

    private void PreencherFinanceiro(ResumoFinanceiroCliente financeiro)
    {
        EmAberto = Formato.Dinheiro(financeiro.EmAberto);
        JaPago = Formato.Dinheiro(financeiro.TotalPago);
        TotalContratado = Formato.Dinheiro(financeiro.TotalContratado);
        TemContratacao = financeiro.TotalContratado > 0;
        PercentualQuitado = financeiro.PercentualQuitado;

        NotaDoFinanceiro = financeiro.UltimoPagamento is { } ultimo
            ? $"{financeiro.PercentualQuitado}% quitado · último em {Formato.Data(ultimo)}"
            : financeiro.TotalContratado > 0 ? "nenhum pagamento lançado" : "sem contratação";
    }

    /// <summary>Os selos do cabeçalho resumem, em uma linha, o que exige atenção.</summary>
    private void PreencherSelos(FichaCliente ficha)
    {
        Selos.Clear();

        var prazo = ficha.Processos
            .Where(p => p.PrazoProtocolo is not null && p.DataProtocolo is null)
            .Select(p => p.PrazoProtocolo!.Value)
            .DefaultIfEmpty()
            .Min();

        if (prazo != default)
        {
            var dias = (int)(prazo.Date - DateTime.Today).TotalDays;
            Selos.Add(Prazos.Selo(Prazos.Texto(dias), dias));
        }

        foreach (var grupo in ficha.Processos.GroupBy(p => p.Fase))
        {
            var nome = Rotulos.Fase(grupo.Key);
            var quantos = grupo.Count();
            var texto = quantos == 1 ? $"1 processo na {nome}" : $"{quantos} processos na {nome}";
            Selos.Add(new SeloViewModel(texto, "RoxoClaro", "Roxo"));
        }

        if (ficha.Financeiro.EmAberto > 0)
        {
            Selos.Add(new SeloViewModel(
                $"{Formato.Dinheiro(ficha.Financeiro.EmAberto)} em aberto", "AlertaClaro", "Alerta"));
        }
    }

    private async Task CarregarVizinhos()
    {
        _paginaVizinhos = 0;
        _totalVizinhos = 0;
        Vizinhos.Clear();

        await CarregarMaisVizinhos();
    }

    /// <summary>Traz o lote seguinte da lista do lado, que rola em vez de paginar.</summary>
    /// <remarks>
    /// Chamado pelo fim da rolagem. Antes vinham 50 fixos: com o escritório crescendo,
    /// o cliente de número 51 simplesmente não existia para quem estava na ficha.
    /// </remarks>
    [RelayCommand]
    private async Task CarregarMaisVizinhos()
    {
        var acabou = _paginaVizinhos > 0 && Vizinhos.Count >= _totalVizinhos;
        if (_carregandoVizinhos || acabou)
        {
            return;
        }

        _carregandoVizinhos = true;

        try
        {
            var pagina = await _clientes.ListarResumoAsync(
                numeroPagina: _paginaVizinhos + 1, tamanhoPagina: PorLote, termo: FiltroDaLista);

            foreach (var resumo in pagina.Itens)
            {
                Vizinhos.Add(new LinhaReduzidaViewModel(resumo) { Aberta = resumo.Id == ClienteId });
            }

            _paginaVizinhos++;
            _totalVizinhos = pagina.TotalRegistros;
            ContagemDaLista = $"{Vizinhos.Count} de {_totalVizinhos}";
        }
        catch
        {
            // A lista do lado é conveniência: se falhar, a ficha continua utilizável.
            if (_paginaVizinhos == 0)
            {
                Vizinhos.Clear();
                ContagemDaLista = string.Empty;
            }
        }
        finally
        {
            _carregandoVizinhos = false;
        }
    }

    /// <summary>O "+ Novo cliente" que agora mora no topo da lista do lado.</summary>
    [RelayCommand]
    private void NovoCliente() => _aoNovoCliente?.Invoke();

    /// <summary>O "editar" de cada cartão, que abre a edição já no grupo daquele cartão.</summary>
    [RelayCommand]
    private void Editar(GrupoCadastro grupo) => _aoEditar?.Invoke(grupo);

    /// <summary>O "+ Novo serviço" do cabeçalho: contratar é o trabalho do dia.</summary>
    /// <remarks>
    /// Mora aqui, e não na aba Serviços, porque todo serviço nasce de uma contratação: é
    /// ela que tem preço. Um cadastro por fora criaria trabalho sem cobrança.
    /// </remarks>
    [RelayCommand]
    private void NovoServico() =>
        Formulario = new NovoServicoViewModel(
            ClienteId,
            aoCancelar: () => Formulario = null,
            aoSalvar: Recarregar);

    /// <summary>Cadastra um veículo e já o liga a este cliente.</summary>
    [RelayCommand]
    private void NovoVeiculo() =>
        Formulario = new VeiculoFormularioViewModel(
            ClienteId,
            aoCancelar: () => Formulario = null,
            aoSalvar: _ => Recarregar());

    [RelayCommand]
    private void EditarVeiculo(VeiculoComMultasViewModel? cartao)
    {
        if (cartao is null)
        {
            return;
        }

        Formulario = new VeiculoFormularioViewModel(
            ClienteId,
            aoCancelar: () => Formulario = null,
            aoSalvar: _ => Recarregar(),
            paraEditar: cartao.Dados);
    }

    /// <summary>Registra uma multa no veículo do cartão.</summary>
    [RelayCommand]
    private void NovaMulta(VeiculoComMultasViewModel? cartao)
    {
        if (cartao is null)
        {
            return;
        }

        Formulario = new MultaFormularioViewModel(
            cartao.Id,
            cartao.Placa,
            aoCancelar: () => Formulario = null,
            aoSalvar: _ => Recarregar());
    }

    [RelayCommand]
    private void EditarMulta(MultaViewModel? multa)
    {
        if (multa is null || AchaVeiculoDaMulta(multa.Id) is not { } cartao)
        {
            return;
        }

        Formulario = new MultaFormularioViewModel(
            cartao.Id,
            cartao.Placa,
            aoCancelar: () => Formulario = null,
            aoSalvar: _ => Recarregar(),
            paraEditar: multa.Dados);
    }

    /// <summary>Pergunta antes de tirar o veículo da ficha, e explica que nada é apagado.</summary>
    [RelayCommand]
    private void PedirDesvinculo(VeiculoComMultasViewModel? cartao)
    {
        if (cartao is null)
        {
            return;
        }

        Confirmacao = new ConfirmacaoViewModel(
            titulo: $"Tirar o veículo {cartao.Placa} desta ficha?",
            texto: "O veículo permanece cadastrado no sistema, com as multas vinculadas "
                 + "a ele. Deixa apenas de constar na ficha deste cliente, e pode ser "
                 + "vinculado novamente a qualquer momento.",
            botao: "Desvincular",
            aoConfirmar: () => Desvincular(cartao.Id),
            aoCancelar: () => Confirmacao = null);
    }

    private async void Desvincular(int veiculoId)
    {
        try
        {
            await new VeiculoDAO().DesvincularDoClienteAsync(ClienteId, veiculoId);
            Confirmacao = null;
            Recarregar();
        }
        catch (Exception excecao)
        {
            Confirmacao = null;
            Erro = $"Não foi possível desvincular: {excecao.Message}";
        }
    }

    /// <summary>Confere quem depende da multa antes de oferecer apagá-la.</summary>
    [RelayCommand]
    private async Task PedirExclusaoMulta(MultaViewModel? multa)
    {
        if (multa is null)
        {
            return;
        }

        var resumo = await new MultaDAO().ResumirExclusaoAsync(multa.Id);

        if (!resumo.PodeApagar)
        {
            Confirmacao = new ConfirmacaoViewModel(
                titulo: $"A multa {multa.Ait} não pode ser apagada",
                texto: MotivoDoBloqueio(resumo),
                botao: null,
                aoConfirmar: null,
                aoCancelar: () => Confirmacao = null);
            return;
        }

        Confirmacao = new ConfirmacaoViewModel(
            titulo: $"Apagar a multa {multa.Ait}?",
            texto: "Não há serviço contratado nem processo de CNH vinculado a esta multa. "
                 + "A exclusão é definitiva e não pode ser desfeita.",
            botao: "Apagar multa",
            aoConfirmar: () => ExcluirMulta(multa.Id),
            aoCancelar: () => Confirmacao = null);
    }

    private static string MotivoDoBloqueio(ResumoExclusaoMulta resumo)
    {
        var partes = new List<string>();

        if (resumo.Recursos > 0)
        {
            partes.Add(resumo.Recursos == 1
                ? "há um serviço contratado sobre ela"
                : $"há {resumo.Recursos} serviços contratados sobre ela");
        }

        if (resumo.ProcessosCnh > 0)
        {
            partes.Add(resumo.ProcessosCnh == 1
                ? "ela integra a pontuação de um processo de CNH"
                : $"ela integra a pontuação de {resumo.ProcessosCnh} processos de CNH");
        }

        // Não oferece "encerre e tente de novo": encerrar o serviço não apaga o que
        // houve, e a multa já cobrada permanece como justificativa da cobrança.
        return $"A exclusão não é permitida porque {string.Join(" e ", partes)}. "
             + "Removê-la deixaria a cobrança sem objeto. Caso o serviço tenha sido "
             + "registrado por engano, encerre-o na aba Serviços: a multa permanece na "
             + "ficha, com o histórico preservado.";
    }

    private async void ExcluirMulta(int multaId)
    {
        try
        {
            await new MultaDAO().ExcluirAsync(multaId);
            Confirmacao = null;
            Recarregar();
        }
        catch (Exception excecao)
        {
            Confirmacao = null;
            Erro = $"Não foi possível apagar a multa: {excecao.Message}";
        }
    }

    /// <summary>A linha da multa não carrega o veículo; aqui se descobre de quem ela é.</summary>
    private VeiculoComMultasViewModel? AchaVeiculoDaMulta(int multaId)
    {
        foreach (var veiculo in VeiculosEMultas)
        {
            foreach (var multa in veiculo.Multas)
            {
                if (multa.Id == multaId)
                {
                    return veiculo;
                }
            }
        }

        return null;
    }

    /// <summary>A lixeira do cabeçalho pergunta antes: arquivar tira o cliente das listas.</summary>
    [RelayCommand]
    private void PedirArquivamento() =>
        Confirmacao = new ConfirmacaoViewModel(
            titulo: "Arquivar este cliente?",
            texto: "O cliente deixa de constar nas listas. O histórico é preservado e o "
                 + "registro fica disponível em Arquivados, de onde pode ser restaurado.",
            botao: "Arquivar",
            aoConfirmar: Arquivar,
            aoCancelar: () => Confirmacao = null);

    private async void Arquivar()
    {
        try
        {
            await _clientes.ArquivarAsync(ClienteId);
            Confirmacao = null;

            // A ficha deixa de fazer sentido aqui: o cliente agora vive em Arquivados.
            _aoVoltar?.Invoke();
        }
        catch (Exception excecao)
        {
            Confirmacao = null;
            Erro = $"Não foi possível arquivar: {excecao.Message}";
        }
    }

    private AbaFichaViewModel AchaAba(AbaFicha aba) => Abas.First(a => a.Aba == aba);

    partial void OnAbaAtualChanged(AbaFicha valor)
    {
        OnPropertyChanged(nameof(AbaDados));
        OnPropertyChanged(nameof(AbaVeiculos));
        OnPropertyChanged(nameof(AbaServicos));
        OnPropertyChanged(nameof(AbaFinanceiro));
        OnPropertyChanged(nameof(AbaDocumentos));
        OnPropertyChanged(nameof(AbaHistorico));
    }

    partial void OnCarregandoVeiculosChanged(bool valor) => OnPropertyChanged(nameof(SemVeiculos));
    partial void OnCarregandoServicosChanged(bool valor) => OnPropertyChanged(nameof(SemServicos));
    partial void OnCarregandoFinanceiroChanged(bool valor) => OnPropertyChanged(nameof(SemLancamentos));
    partial void OnCarregandoHistoricoChanged(bool valor) => OnPropertyChanged(nameof(SemHistorico));

    private static string MontarIdentificacao(Cliente cliente)
    {
        var partes = new List<string> { $"CPF {Formato.Cpf(cliente.Cpf)}" };

        if (cliente.DataCadastro != default)
        {
            partes.Add($"cadastrado em {Formato.Data(cliente.DataCadastro)}");
        }

        if (!string.IsNullOrWhiteSpace(cliente.Whatsapp))
        {
            partes.Add(Formato.Telefone(cliente.Whatsapp));
        }

        if (cliente.Endereco is { } endereco && !string.IsNullOrWhiteSpace(endereco.Cidade))
        {
            partes.Add($"{endereco.Cidade}/{endereco.Estado}");
        }

        return string.Join(" · ", partes);
    }

    private static string? JuntarRg(string? rg, string? orgao) =>
        string.IsNullOrWhiteSpace(rg) ? null
        : string.IsNullOrWhiteSpace(orgao) ? Formato.Rg(rg) : $"{Formato.Rg(rg)} {orgao}";

    private static string? MontarLogradouro(Endereco? endereco)
    {
        if (endereco is null || string.IsNullOrWhiteSpace(endereco.Logradouro))
        {
            return null;
        }

        var texto = endereco.Logradouro;
        if (!string.IsNullOrWhiteSpace(endereco.Numero)) texto += $", {endereco.Numero}";
        if (!string.IsNullOrWhiteSpace(endereco.Complemento)) texto += $", {endereco.Complemento}";

        return texto;
    }

    private static string? MontarCidade(Endereco? endereco) =>
        endereco is null || string.IsNullOrWhiteSpace(endereco.Cidade)
            ? null
            : $"{endereco.Cidade} — {endereco.Estado}";
}

/// <summary>Um par rótulo/valor dos cartões da aba Dados.</summary>
public class CampoFicha(string rotulo, string? valor)
{
    public string Rotulo { get; } = rotulo;

    /// <summary>Campo vazio aparece como "não informado", em cinza — e não em branco.</summary>
    public string Valor { get; } = string.IsNullOrWhiteSpace(valor) ? "não informado" : valor;

    public bool Preenchido { get; } = !string.IsNullOrWhiteSpace(valor);
}

public class VeiculoDaFichaViewModel(VeiculoDaFicha dados)
{
    public string Placa { get; } = dados.Placa;

    public string Descricao { get; } = string.Join(" · ", new[]
    {
        dados.MarcaModelo,
        dados.Ano,
        dados.Cor,
        dados.Vinculo == "PROPRIETARIO" ? "proprietário" : "condutor"
    }.Where(p => !string.IsNullOrWhiteSpace(p)));

    public string Multas { get; } = dados.QuantidadeMultas switch
    {
        0 => "sem multas",
        1 => "1 multa",
        var quantas => $"{quantas} multas"
    };

    public bool TemMultas { get; } = dados.QuantidadeMultas > 0;
}

public class ProcessoDaFichaViewModel(ProcessoDaFicha dados)
{
    public string Titulo { get; } = string.IsNullOrWhiteSpace(dados.Ait)
        ? dados.Servico
        : $"{dados.Servico} — AIT {dados.Ait}";

    public string Detalhe { get; } = string.Join(" · ", new[]
    {
        string.IsNullOrWhiteSpace(dados.Placa) ? null : $"Placa {dados.Placa}",
        string.IsNullOrWhiteSpace(dados.Enquadramento) ? null : $"art. {dados.Enquadramento}",
        dados.DataProtocolo is { } protocolo
            ? $"protocolado em {Formato.Data(protocolo)}"
            : TextoDaSituacao(dados.Situacao)
    }.Where(p => !string.IsNullOrWhiteSpace(p)));

    public string Fase { get; } = Rotulos.Fase(dados.Fase);

    /// <summary>Selo do prazo, na mesma régua de cor da lista de clientes.</summary>
    public SeloViewModel SeloPrazo { get; } = MontarSeloPrazo(dados);

    private static SeloViewModel MontarSeloPrazo(ProcessoDaFicha dados)
    {
        if (dados.DataProtocolo is not null)
        {
            return Prazos.Selo("protocolado", null);
        }

        if (dados.PrazoProtocolo is not { } prazo)
        {
            return Prazos.Selo("sem prazo", null);
        }

        var dias = (int)(prazo.Date - DateTime.Today).TotalDays;
        return Prazos.Selo($"prazo {prazo:dd/MM}", dias);
    }

    private static string TextoDaSituacao(string situacao) => Rotulos.Situacao(situacao);
}
