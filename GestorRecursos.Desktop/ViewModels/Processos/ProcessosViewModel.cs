using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>A tela de Processos: tudo que corre no escritório, de todos os clientes.</summary>
/// <remarks>
/// A ficha responde "o que este cliente contratou", e para acompanhar o escritório era
/// preciso abrir cliente por cliente. O painel do dia responde "o que vence agora", mas só
/// isso: quem já protocolou e espera julgamento some de lá, e com razão — não há o que
/// fazer com ele hoje.
///
/// Esta tela é a terceira pergunta, a que faltava: <b>onde está cada processo</b>. Uma
/// linha por processo, o cliente dentro da linha, e a busca varrendo nome, CPF, AIT, placa
/// e número de protocolo de uma vez — porque o cliente liga perguntando por qualquer um
/// desses, e não se sabe de antemão por qual.
///
/// Abrir a linha mostra as fases ali mesmo, com os botões de protocolo e resultado: a
/// lista é de trabalho, não de leitura, e mandar a pessoa à ficha para registrar o que ela
/// acabou de ver seria devolvê-la ao problema que esta tela resolve.
///
/// Os filtros são listas, e não botões: seis grupos de botões lado a lado comiam a altura
/// da lista, que é o que de fato se olha o dia inteiro.
/// </remarks>
public partial class ProcessosViewModel : ViewModelBase
{
    // Os rótulos são o próprio valor do filtro. Guardar um enum ao lado significaria
    // manter duas listas em sincronia para ganhar nada: nenhum deles vai para o banco.
    public const string SituacaoAbertos = "Em aberto";
    public const string SituacaoVencidos = "Com prazo vencido";
    public const string SituacaoAguardando = "Aguardando julgamento";
    public const string SituacaoEncerrados = "Encerrados";
    public const string SituacaoTodas = "Todas as situações";

    public const string TodasAsInstancias = "Todas as instâncias";
    public const string InstanciaAutuacao = "Autuação";
    public const string InstanciaJari = "JARI";
    public const string InstanciaCetran = "CETRAN";

    public const string TodosOsTipos = "Todos os tipos";
    public const string TipoMulta = "Recurso de multa";
    public const string TipoCnh = "Processo de CNH";
    public const string TipoDireto = "Serviço direto";

    public const string TodosOsOrgaos = "Todos os órgãos";

    private readonly ProcessoDAO _processos = new();
    private readonly Action<int>? _aoAbrirCliente;

    private List<LinhaProcessoViewModel> _todos = [];

    /// <summary>Quais linhas estavam abertas, para a recarga não fechar o que se lia.</summary>
    private readonly HashSet<int> _abertas = [];

    public ObservableCollection<LinhaProcessoViewModel> Linhas { get; } = [];

    public ProcessosViewModel(Action<int>? aoAbrirCliente = null)
    {
        _aoAbrirCliente = aoAbrirCliente;

        // No preview do Rider não há banco nem wallet: a tela abriria com erro de conexão.
        if (!Design.IsDesignMode)
        {
            CarregarCommand.Execute(null);
        }
    }

    public IReadOnlyList<string> Situacoes { get; } =
        [SituacaoAbertos, SituacaoVencidos, SituacaoAguardando, SituacaoEncerrados, SituacaoTodas];

    public IReadOnlyList<string> Instancias { get; } =
        [TodasAsInstancias, InstanciaAutuacao, InstanciaJari, InstanciaCetran];

    public IReadOnlyList<string> Tipos { get; } =
        [TodosOsTipos, TipoMulta, TipoCnh, TipoDireto];

    /// <summary>Os órgãos que aparecem nos processos, para não digitar o nome à mão.</summary>
    public ObservableCollection<string> Orgaos { get; } = [TodosOsOrgaos];

    [ObservableProperty]
    public partial string SituacaoEscolhida { get; set; } = SituacaoAbertos;

    [ObservableProperty]
    public partial string InstanciaEscolhida { get; set; } = TodasAsInstancias;

    [ObservableProperty]
    public partial string TipoEscolhido { get; set; } = TodosOsTipos;

    [ObservableProperty]
    public partial string OrgaoEscolhido { get; set; } = TodosOsOrgaos;

    [ObservableProperty]
    public partial string PrazoDe { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PrazoAte { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ContratadoDe { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ContratadoAte { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string TermoBusca { get; set; } = string.Empty;

    /// <summary>A barra de filtros está aberta. Fechada, a lista ganha a altura de volta.</summary>
    /// <remarks>
    /// Retrátil porque a filtragem é escolha de minutos e a lista é olhada o dia inteiro.
    /// </remarks>
    [ObservableProperty]
    public partial bool FiltrosAbertos { get; set; }

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? Erro { get; set; }

    /// <summary>Formulário de protocolo ou resultado, por cima da lista.</summary>
    [ObservableProperty]
    public partial ViewModelBase? Formulario { get; set; }

    [ObservableProperty]
    public partial int QuantosAbertos { get; set; }

    [ObservableProperty]
    public partial int QuantosVencidos { get; set; }

    [ObservableProperty]
    public partial int QuantosAguardando { get; set; }

    [ObservableProperty]
    public partial int QuantosEncerrados { get; set; }

    [ObservableProperty]
    public partial string Contagem { get; set; } = string.Empty;

    public bool TemBusca => TermoBusca.Length > 0;
    public bool SemResultado => !Carregando && Erro is null && Linhas.Count == 0;

    /// <summary>Algum filtro saiu do padrão. Serve só à mensagem da lista vazia.</summary>
    private bool AlgumFiltro =>
        SituacaoEscolhida != SituacaoAbertos
        || InstanciaEscolhida != TodasAsInstancias
        || TipoEscolhido != TodosOsTipos
        || OrgaoEscolhido != TodosOsOrgaos
        || Formato.LerData(PrazoDe) is not null
        || Formato.LerData(PrazoAte) is not null
        || Formato.LerData(ContratadoDe) is not null
        || Formato.LerData(ContratadoAte) is not null;

    /// <summary>A mensagem da lista vazia muda com o motivo de ela estar vazia.</summary>
    public string MensagemVazia
    {
        get
        {
            if (TemBusca)
            {
                return "Nenhum processo encontrado para o que foi digitado.";
            }

            if (AlgumFiltro && SituacaoEscolhida == SituacaoAbertos)
            {
                return "Nenhum processo atende aos filtros escolhidos.";
            }

            return SituacaoEscolhida switch
            {
                SituacaoVencidos => "Nenhum prazo vencido. É o melhor resultado possível aqui.",
                SituacaoAguardando => "Nenhuma peça esperando julgamento no momento.",
                SituacaoEncerrados => "Nenhum processo encerrado até agora.",
                SituacaoAbertos => "Nenhum processo em aberto. Tudo que foi contratado já terminou.",
                _ => "Nenhum processo cadastrado ainda."
            };
        }
    }

    [RelayCommand]
    private async Task Carregar()
    {
        Carregando = true;
        Erro = null;

        try
        {
            var processos = await _processos.ListarAsync();

            _todos = [.. processos.Select(p => new LinhaProcessoViewModel(p))];

            // Reabre o que estava aberto antes da recarga: quem registrou um protocolo
            // quer ver a fase mudar, e não perder de vista a linha em que estava.
            foreach (var linha in _todos.Where(l => _abertas.Contains(l.ItemId)))
            {
                linha.Aberto = true;
            }

            QuantosAbertos = _todos.Count(l => !l.Encerrado);
            QuantosVencidos = _todos.Count(l => l.Vencido);
            QuantosAguardando = _todos.Count(l => l.AguardandoJulgamento);
            QuantosEncerrados = _todos.Count(l => l.Encerrado);

            MontarOrgaos();
            AplicarFiltro();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível carregar os processos: {excecao.Message}";
        }
        finally
        {
            Carregando = false;
            OnPropertyChanged(nameof(SemResultado));
        }
    }

    [RelayCommand]
    private void AlternarFiltros() => FiltrosAbertos = !FiltrosAbertos;

    /// <summary>Devolve a lista inteira, sem fechar a barra: quase sempre vem outro filtro.</summary>
    [RelayCommand]
    private void LimparFiltros()
    {
        SituacaoEscolhida = SituacaoAbertos;
        InstanciaEscolhida = TodasAsInstancias;
        TipoEscolhido = TodosOsTipos;
        OrgaoEscolhido = TodosOsOrgaos;
        PrazoDe = string.Empty;
        PrazoAte = string.Empty;
        ContratadoDe = string.Empty;
        ContratadoAte = string.Empty;
    }

    // Escolher na lista ou digitar uma data refaz o corte na hora, como a busca faz.
    partial void OnSituacaoEscolhidaChanged(string value) => AplicarFiltro();
    partial void OnInstanciaEscolhidaChanged(string value) => AplicarFiltro();
    partial void OnTipoEscolhidoChanged(string value) => AplicarFiltro();
    partial void OnOrgaoEscolhidoChanged(string value) => AplicarFiltro();
    partial void OnPrazoDeChanged(string value) => AplicarFiltro();
    partial void OnPrazoAteChanged(string value) => AplicarFiltro();
    partial void OnContratadoDeChanged(string value) => AplicarFiltro();
    partial void OnContratadoAteChanged(string value) => AplicarFiltro();

    /// <summary>Busca a cada tecla: a lista já está na memória, não há ida ao banco.</summary>
    partial void OnTermoBuscaChanged(string value)
    {
        AplicarFiltro();
        OnPropertyChanged(nameof(TemBusca));
    }

    [RelayCommand]
    private void LimparBusca() => TermoBusca = string.Empty;

    /// <summary>Abre ou fecha as fases de uma linha, ali mesmo na lista.</summary>
    [RelayCommand]
    private void AlternarDetalhe(LinhaProcessoViewModel? linha)
    {
        if (linha is null)
        {
            return;
        }

        linha.Aberto = !linha.Aberto;

        if (linha.Aberto)
        {
            _abertas.Add(linha.ItemId);
        }
        else
        {
            _abertas.Remove(linha.ItemId);
        }
    }

    /// <summary>Leva à ficha do cliente, para o que esta tela não mostra.</summary>
    [RelayCommand]
    private void AbrirCliente(int clienteId) => _aoAbrirCliente?.Invoke(clienteId);

    /// <summary>Registra o protocolo de uma fase sem sair da lista.</summary>
    [RelayCommand]
    private void RegistrarProtocolo(FaseViewModel? fase) => AbrirAndamento(AcaoAndamento.Protocolo, fase);

    /// <summary>Registra o julgamento, ou anula a fase. Os dois abrem a seguinte sozinhos.</summary>
    [RelayCommand]
    private void RegistrarResultado(FaseViewModel? fase) => AbrirAndamento(AcaoAndamento.Resultado, fase);

    private void AbrirAndamento(AcaoAndamento acao, FaseViewModel? fase)
    {
        if (fase?.Servico is not { } servico)
        {
            return;
        }

        Formulario = new AndamentoFormularioViewModel(
            acao, servico, fase,
            aoCancelar: () => Formulario = null,
            aoSalvar: () =>
            {
                Formulario = null;
                CarregarCommand.Execute(null);
            });
    }

    /// <summary>Marca ou desmarca uma etapa do roteiro de um serviço direto.</summary>
    /// <remarks>
    /// Grava na hora e não recarrega a lista, como na ficha: marcar etapa é gesto solto e
    /// repetido, e recarregar a cada clique faria a tela inteira piscar.
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

            etapa.AplicarMarca(alvo, alvo ? DateTime.Today : null);
            etapa.Servico?.Recontar();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível marcar a etapa: {excecao.Message}";
        }
    }

    /// <summary>Cada filtro corta o que o anterior deixou, e a busca corta por último.</summary>
    /// <remarks>
    /// Nesta ordem de propósito: quem digita um nome com "com prazo vencido" escolhido
    /// quer os vencidos daquela pessoa, e não todos os processos dela.
    ///
    /// A ordenação é a da urgência — aberto antes de encerrado, e dentro dos abertos o
    /// prazo mais apertado primeiro. Processo sem prazo correndo vai para o fim da sua
    /// turma, e não para o começo, que é onde um "nulo" cairia sem o cuidado abaixo.
    /// </remarks>
    private void AplicarFiltro()
    {
        var filtradas = SituacaoEscolhida switch
        {
            SituacaoAbertos => _todos.Where(l => !l.Encerrado),
            SituacaoVencidos => _todos.Where(l => l.Vencido),
            SituacaoAguardando => _todos.Where(l => l.AguardandoJulgamento),
            SituacaoEncerrados => _todos.Where(l => l.Encerrado),
            _ => _todos
        };

        filtradas = InstanciaEscolhida switch
        {
            InstanciaAutuacao => filtradas.Where(l => l.FaseAtual == FaseInstancia.Autuacao),
            InstanciaJari => filtradas.Where(l => l.FaseAtual == FaseInstancia.Jari),
            InstanciaCetran => filtradas.Where(l => l.FaseAtual == FaseInstancia.Cetran),
            _ => filtradas
        };

        filtradas = TipoEscolhido switch
        {
            TipoMulta => filtradas.Where(l => l.DeMulta),
            TipoCnh => filtradas.Where(l => l.DeCnh),
            TipoDireto => filtradas.Where(l => l.Direto),
            _ => filtradas
        };

        if (OrgaoEscolhido != TodosOsOrgaos)
        {
            filtradas = filtradas.Where(l => l.Orgao == OrgaoEscolhido);
        }

        // Data mal digitada não filtra nada: enquanto "11/1" não é uma data, a lista
        // continua inteira em vez de esvaziar no meio da digitação.
        filtradas = EntreDatas(filtradas, l => l.PrazoData, PrazoDe, PrazoAte);
        filtradas = EntreDatas(filtradas, l => l.ContratadoEm, ContratadoDe, ContratadoAte);

        var termo = TermoBusca.Trim().ToLowerInvariant();
        if (termo.Length > 0)
        {
            filtradas = filtradas.Where(l => l.Busca.Contains(termo, StringComparison.Ordinal));
        }

        var ordenadas = filtradas
            .OrderBy(l => l.Encerrado)
            .ThenBy(l => l.DiasAteOPrazo ?? int.MaxValue)
            .ThenByDescending(l => l.ContratadoEm)
            .ToList();

        Linhas.Clear();
        foreach (var linha in ordenadas)
        {
            Linhas.Add(linha);
        }

        Contagem = MontarContagem(ordenadas.Count);

        OnPropertyChanged(nameof(SemResultado));
        OnPropertyChanged(nameof(MensagemVazia));
    }

    /// <summary>Corta pelo período, quando a data digitada já é uma data.</summary>
    private static IEnumerable<LinhaProcessoViewModel> EntreDatas(
        IEnumerable<LinhaProcessoViewModel> linhas,
        Func<LinhaProcessoViewModel, DateTime?> quando,
        string de,
        string ate)
    {
        if (Formato.LerData(de) is { } inicio)
        {
            linhas = linhas.Where(l => quando(l) is { } data && data.Date >= inicio.Date);
        }

        if (Formato.LerData(ate) is { } fim)
        {
            linhas = linhas.Where(l => quando(l) is { } data && data.Date <= fim.Date);
        }

        return linhas;
    }

    /// <summary>Os órgãos que de fato aparecem nos processos, em ordem alfabética.</summary>
    /// <remarks>
    /// Tirados dos dados, e não de uma tabela: o órgão é texto livre em MULTAS e em
    /// INSTANCIAS, então a única lista verdadeira é a do que já foi digitado. Se o
    /// escolhido desaparecer numa recarga, a escolha volta para "todos" em vez de
    /// deixar a tela filtrando por algo que não existe mais.
    /// </remarks>
    private void MontarOrgaos()
    {
        var encontrados = _todos
            .Select(l => l.Orgao)
            .Where(o => !string.IsNullOrWhiteSpace(o))
            .Select(o => o!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(o => o, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        Orgaos.Clear();
        Orgaos.Add(TodosOsOrgaos);
        foreach (var orgao in encontrados)
        {
            Orgaos.Add(orgao);
        }

        if (!Orgaos.Contains(OrgaoEscolhido))
        {
            OrgaoEscolhido = TodosOsOrgaos;
        }
    }

    private string MontarContagem(int mostrados)
    {
        if (_todos.Count == 0)
        {
            return string.Empty;
        }

        var nome = mostrados == 1 ? "processo" : "processos";

        return mostrados == _todos.Count
            ? $"{mostrados} {nome}"
            : $"{mostrados} {nome} de {_todos.Count}";
    }
}
