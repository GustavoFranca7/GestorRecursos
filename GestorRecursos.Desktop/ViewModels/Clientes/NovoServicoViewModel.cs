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

/// <summary>As telas por onde a contratação pode passar.</summary>
/// <remarks>
/// Não é uma sequência fixa: o serviço escolhido no primeiro passo decide quais destes
/// entram no caminho. Renovação de CNH não tem o que perguntar sobre carro nem multa.
/// </remarks>
public enum PassoServico
{
    Servico,
    Veiculo,
    Multas,
    Processo,
    Preco,
    Pagamento
}

/// <summary>Uma multa do cliente na seleção da pontuação, no passo do processo de CNH.</summary>
public partial class MultaDaPontuacaoViewModel : ObservableObject
{
    private readonly Action _aoMarcar;

    public MultaDaPontuacaoViewModel(Multa dados, Action aoMarcar)
    {
        Dados = dados;
        _aoMarcar = aoMarcar;

        var gravidade = dados.Gravidade?.Texto();
        var (fundo, texto) = Rotulos.CoresDaGravidade(gravidade);
        Gravidade = Rotulos.Gravidade(gravidade, dados.Pontos);
        FundoGravidade = fundo;
        CorGravidade = texto;

        Placa = dados.Veiculo?.Placa ?? string.Empty;
        Descricao = string.IsNullOrWhiteSpace(dados.Descricao)
            ? dados.Enquadramento ?? "sem descrição"
            : dados.Descricao;
        Quando = dados.DataHoraInfracao is { } momento ? Formato.Data(momento) : "sem data";
    }

    public Multa Dados { get; }

    [ObservableProperty] public partial bool Marcada { get; set; }

    public int Id => Dados.Id;
    public int Pontos => Dados.Pontos ?? 0;
    public string Ait => Dados.Ait;
    public string Placa { get; }
    public string Descricao { get; }
    public string Quando { get; }
    public string Gravidade { get; }
    public string FundoGravidade { get; }
    public string CorGravidade { get; }
    public bool TemGravidade => Gravidade.Length > 0;

    partial void OnMarcadaChanged(bool valor) => _aoMarcar();
}

/// <summary>Um item da contratação no passo do preço, com o serviço e o valor dele.</summary>
/// <remarks>
/// Serve aos quatro caminhos. Num recurso, cada multa marcada vira um item e o serviço
/// vem sugerido pela gravidade; nos outros, o item é um só e representa o próprio serviço.
/// O seletor fica em todos porque é onde a variante é escolhida — gravíssima 3x, defesa
/// com indicação, reciclagem completa.
/// </remarks>
public partial class ItemDoServicoViewModel : ObservableObject
{
    private readonly Action _aoTrocar;

    public ItemDoServicoViewModel(
        string identificacao,
        string selo,
        string fundoSelo,
        string corSelo,
        IReadOnlyList<TipoServico> servicos,
        TipoServico? sugerido,
        int? multaId,
        DateTime? prazo,
        Action aoTrocar)
    {
        _aoTrocar = aoTrocar;
        Identificacao = identificacao;
        Selo = selo;
        FundoSelo = fundoSelo;
        CorSelo = corSelo;
        Servicos = servicos;
        MultaId = multaId;
        Prazo = prazo;
        Servico = sugerido ?? (servicos.Count == 1 ? servicos[0] : null);
    }

    /// <summary>O que identifica o item: AIT com placa, número do processo ou a origem.</summary>
    public string Identificacao { get; }

    /// <summary>Gravidade da multa ou tipo do processo. Vazio quando não se aplica.</summary>
    public string Selo { get; }
    public string FundoSelo { get; }
    public string CorSelo { get; }
    public bool TemSelo => Selo.Length > 0;

    /// <summary>O serviço da tabela, decidido pela gravidade. Não se escolhe aqui.</summary>
    public string Nome => Servico?.Nome ?? "—";
    public int? MultaId { get; }

    /// <summary>Prazo que a primeira instância vai vigiar, quando o serviço tiver fases.</summary>
    public DateTime? Prazo { get; }

    public IReadOnlyList<TipoServico> Servicos { get; }

    [ObservableProperty] public partial TipoServico? Servico { get; set; }

    public decimal Valor => Servico?.ValorTabela ?? 0m;
    public string ValorTexto => Servico is null ? "—" : Formato.Dinheiro(Valor);

    partial void OnServicoChanged(TipoServico? valor)
    {
        OnPropertyChanged(nameof(Nome));
        OnPropertyChanged(nameof(ValorTexto));
        _aoTrocar();
    }
}

/// <summary>Contrata um serviço do começo ao fim, pelo caminho que ele exigir.</summary>
/// <remarks>
/// A janela antiga só sabia fazer recurso de multa: pedia carro e auto mesmo para uma
/// renovação de CNH, e sem multa para marcar não deixava contratar nada. Agora o primeiro
/// passo é o serviço, e é ele que decide se vai existir passo de veículo, de multa ou de
/// processo do DETRAN.
///
/// Veículo, multa e processo que nascem aqui são gravados pelos formulários de sempre, na
/// hora: existem no mundo real mesmo que o negócio não feche. Só o que é dinheiro —
/// contratação, itens, instâncias, etapas e pagamento — espera o fim e vai numa transação.
/// </remarks>
public partial class NovoServicoViewModel : ViewModelBase
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private readonly FichaClienteDAO _fichas = new();
    private readonly MultaDAO _multas = new();
    private readonly TipoServicoDAO _tiposServico = new();
    private readonly ContratacaoDAO _contratacoes = new();
    private readonly ProcessoCnhDAO _processos = new();

    private readonly int _clienteId;
    private readonly Action? _aoCancelar;
    private readonly Action? _aoSalvar;

    private IReadOnlyList<TipoServico> _tabela = [];
    private List<PassoServico> _caminho = [PassoServico.Servico];

    /// <summary>Evita que o recálculo do preço apague o valor que ela acabou de digitar.</summary>

    public NovoServicoViewModel(int clienteId, Action? aoCancelar = null, Action? aoSalvar = null)
    {
        _clienteId = clienteId;
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;

        if (!Design.IsDesignMode)
        {
            CarregarCommand.Execute(null);
        }
    }

    public ObservableCollection<PassoViewModel> Passos { get; } = [];
    public ObservableCollection<OpcaoServicoViewModel> Opcoes { get; } = [];
    public ObservableCollection<VeiculoDoServicoViewModel> Veiculos { get; } = [];
    public ObservableCollection<MultaDoServicoViewModel> Multas { get; } = [];
    public ObservableCollection<MultaDaPontuacaoViewModel> MultasDoCliente { get; } = [];
    public ObservableCollection<ItemDoServicoViewModel> Itens { get; } = [];

    /// <summary>Custos adiantados pelo escritório e repassados ao cliente.</summary>
    /// <remarks>
    /// Nome e valor são livres porque a lista muda: correio hoje, taxa de DETRAN amanhã,
    /// autenticação de firma quando for o caso. Entram no que o cliente deve e saem do
    /// resultado do escritório, que é o combinado dos serviços.
    /// </remarks>
    public ObservableCollection<DespesaDoServicoViewModel> Despesas { get; } = [];

    public IReadOnlyList<string> Formas { get; } =
        ["Dinheiro", "PIX", "PIX parcelado", "Débito", "Crédito"];

    public IReadOnlyList<string> TiposDeProcesso { get; } = ["Suspensão", "Cassação"];

    // ---------- estado da janela ----------
    [ObservableProperty] public partial PassoServico Passo { get; set; } = PassoServico.Servico;
    [ObservableProperty] public partial bool Carregando { get; set; }
    [ObservableProperty] public partial bool Salvando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial ViewModelBase? SubFormulario { get; set; }

    // ---------- passo 1: serviço ----------
    [ObservableProperty] public partial string BuscaServico { get; set; } = string.Empty;
    [ObservableProperty] public partial OpcaoServicoViewModel? Escolha { get; set; }

    // ---------- veículo ----------
    [ObservableProperty] public partial VeiculoDoServicoViewModel? Veiculo { get; set; }

    // ---------- processo de CNH ----------
    [ObservableProperty] public partial string NumeroProcesso { get; set; } = string.Empty;
    [ObservableProperty] public partial string? TipoDeProcesso { get; set; } = "Suspensão";
    [ObservableProperty] public partial string DataNotificacao { get; set; } = string.Empty;
    [ObservableProperty] public partial string PrazoDoProcesso { get; set; } = string.Empty;
    [ObservableProperty] public partial string OrgaoInstaurador { get; set; } = "DETRAN/SC";
    [ObservableProperty] public partial string Portaria { get; set; } = string.Empty;
    [ObservableProperty] public partial string? ErroNumero { get; set; }
    [ObservableProperty] public partial string? ErroPrazo { get; set; }

    // ---------- preço ----------
    [ObservableProperty] public partial string DescontoTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string MotivoDesconto { get; set; } = string.Empty;
    [ObservableProperty] public partial string Observacoes { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovaDespesa { get; set; } = string.Empty;
    [ObservableProperty] public partial string NovaDespesaValor { get; set; } = string.Empty;

    // ---------- pagamento ----------
    [ObservableProperty] public partial bool PagarAgora { get; set; } = true;
    [ObservableProperty] public partial string? Forma { get; set; } = "PIX";
    [ObservableProperty] public partial string ValorPagoTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string Parcelas { get; set; } = "1";
    [ObservableProperty] public partial string ObservacaoPagamento { get; set; } = string.Empty;

    public bool NoServico => Passo == PassoServico.Servico;
    public bool NoVeiculo => Passo == PassoServico.Veiculo;
    public bool NasMultas => Passo == PassoServico.Multas;
    public bool NoProcesso => Passo == PassoServico.Processo;
    public bool NoPreco => Passo == PassoServico.Preco;
    public bool NoPagamento => Passo == PassoServico.Pagamento;

    public bool TemErro => Erro is not null;
    public bool SemVeiculos => !Carregando && Veiculos.Count == 0;
    public bool SemMultas => !Carregando && Multas.Count == 0;
    public bool SemMultasDoCliente => !Carregando && MultasDoCliente.Count == 0;

    public string Titulo => "Novo serviço";

    public string Instrucao => Passo switch
    {
        PassoServico.Servico => "Selecione o serviço a ser contratado.",
        PassoServico.Veiculo => "Selecione o veículo do auto de infração.",
        PassoServico.Multas => "Marque as multas que compõem este serviço.",
        PassoServico.Processo => "Dados do processo instaurado pelo DETRAN e multas que compõem a pontuação.",
        PassoServico.Preco => "Confira o serviço e defina o valor combinado.",
        _ => "Forma de pagamento da contratação."
    };

    /// <summary>No primeiro passo ainda não se sabe quantos passos são: a trilha só nasce depois.</summary>
    public bool MostrarTrilha => Passo != PassoServico.Servico;

    public string Contador => Passo == PassoServico.Servico
        ? "Passo 1"
        : $"Passo {_caminho.IndexOf(Passo) + 1} de {_caminho.Count}";

    public bool PodeVoltar => Passo != PassoServico.Servico;

    public string TextoAvancar => Passo == PassoServico.Pagamento ? "Contratar" : "Avançar →";

    /// <summary>O serviço escolhido, repetido no topo dos passos seguintes.</summary>
    public string NomeDoServico => Escolha?.Titulo ?? string.Empty;
    public bool TemEscolha => Escolha is not null;

    public string PlacaEscolhida => Veiculo?.Placa ?? string.Empty;
    public bool TemVeiculo => Veiculo is not null;

    public int Marcadas => Multas.Count(m => m.Marcada);

    public string ResumoMarcadas => Marcadas switch
    {
        0 => "nenhuma multa marcada",
        1 => "1 multa marcada",
        _ => $"{Marcadas} multas marcadas"
    };

    public int PontosMarcados => MultasDoCliente.Where(m => m.Marcada).Sum(m => m.Pontos);

    public string ResumoPontuacao
    {
        get
        {
            var quantas = MultasDoCliente.Count(m => m.Marcada);
            return quantas == 0
                ? "nenhuma multa marcada"
                : $"{(quantas == 1 ? "1 multa marcada" : $"{quantas} multas marcadas")} · {PontosMarcados} pontos";
        }
    }

    public decimal TotalTabela => Itens.Sum(i => i.Valor);
    public string TotalTabelaTexto => Formato.Dinheiro(TotalTabela);

    public decimal TotalDespesas => Despesas.Sum(d => d.Valor);
    public string TotalDespesasTexto => Formato.Dinheiro(TotalDespesas);
    public bool TemDespesas => Despesas.Count > 0;

    /// <summary>Quanto o desconto tirou da tabela. Digitado, e não deduzido do total.</summary>
    /// <remarks>
    /// Antes ela digitava o valor fechado e o desconto saía da diferença. Invertido, o que
    /// fica registrado é o abatimento concedido — que é o dado que ela quer rever depois.
    /// </remarks>
    public decimal Desconto => Math.Min(Math.Max(0m, Formato.LerDinheiro(DescontoTexto) ?? 0m), TotalTabela);

    /// <summary>O combinado pelos serviços, antes das despesas repassadas.</summary>
    public decimal ValorServicos => TotalTabela - Desconto;
    public string ValorServicosTexto => Formato.Dinheiro(ValorServicos);

    /// <summary>O que o cliente deve: os serviços mais o que o escritório adianta por ele.</summary>
    public decimal ValorFinal => ValorServicos + TotalDespesas;
    public string ValorFinalTotalTexto => Formato.Dinheiro(ValorFinal);

    // O sinal no resumo: sem ele, uma coluna de valores não diz o que soma e o que abate.
    public string DescontoResumo => $"− {Formato.Dinheiro(Desconto)}";
    public string DespesasResumo => $"+ {Formato.Dinheiro(TotalDespesas)}";
    public bool TemDesconto => Desconto > 0;

    public bool Parcelavel => Forma is "Crédito" or "PIX parcelado";

    public string ResumoFinal => $"{NomeDoServico} · {Formato.Dinheiro(ValorFinal)}";

    [RelayCommand]
    private void Cancelar() => _aoCancelar?.Invoke();

    /// <summary>Põe mais um custo na conta da contratação.</summary>
    [RelayCommand]
    private void AdicionarDespesa()
    {
        var nome = NovaDespesa.Trim();
        if (nome.Length == 0 || Formato.LerDinheiro(NovaDespesaValor) is not { } valor || valor <= 0)
        {
            Erro = "Informe o nome e o valor da despesa.";
            return;
        }

        Despesas.Add(new DespesaDoServicoViewModel(nome, valor));
        NovaDespesa = string.Empty;
        NovaDespesaValor = string.Empty;
        Erro = null;
        RecalcularPreco();
    }

    [RelayCommand]
    private void RemoverDespesa(DespesaDoServicoViewModel? despesa)
    {
        if (despesa is not null && Despesas.Remove(despesa))
        {
            RecalcularPreco();
        }
    }

    private void RecalcularPreco()
    {
        OnPropertyChanged(nameof(TotalDespesas));
        OnPropertyChanged(nameof(TotalDespesasTexto));
        OnPropertyChanged(nameof(DespesasResumo));
        OnPropertyChanged(nameof(TemDespesas));
        AtualizarDinheiro();
    }

    [RelayCommand]
    private async Task Carregar()
    {
        Carregando = true;
        Erro = null;

        try
        {
            _tabela = await _tiposServico.ListarAsync();
            MontarOpcoes();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível abrir a lista de serviços: {excecao.Message}";
        }
        finally
        {
            Carregando = false;
        }
    }

    // ===================== passo 1: serviço =====================

    /// <summary>Monta a lista: um item por fluxo que agrupa, um por serviço nos demais.</summary>
    private void MontarOpcoes()
    {
        var termo = BuscaServico.Trim();
        var escolhido = Escolha?.Titulo;

        Opcoes.Clear();

        foreach (var fluxo in Ordem)
        {
            var doFluxo = _tabela.Where(s => s.Fluxo == fluxo).OrderBy(s => s.ValorTabela).ToList();
            if (doFluxo.Count == 0)
            {
                continue;
            }

            if (fluxo.AgrupaVariantes())
            {
                Acolher(new OpcaoServicoViewModel(fluxo, doFluxo), termo, escolhido);
                continue;
            }

            foreach (var servico in doFluxo.OrderBy(s => s.Nome))
            {
                Acolher(new OpcaoServicoViewModel(servico), termo, escolhido);
            }
        }

        OnPropertyChanged(nameof(Opcoes));
    }

    private void Acolher(OpcaoServicoViewModel opcao, string termo, string? escolhido)
    {
        if (!opcao.Combina(termo))
        {
            return;
        }

        opcao.Escolhido = opcao.Titulo == escolhido;
        Opcoes.Add(opcao);
    }

    /// <summary>A ordem dos grupos na tela: multas primeiro, que é o grosso do trabalho.</summary>
    private static readonly FluxoServico[] Ordem =
    [
        FluxoServico.RecursoDeMulta,
        FluxoServico.Indicacao,
        FluxoServico.DefesaCnh,
        FluxoServico.ServicoCnh,
        FluxoServico.SobreVeiculo,
        FluxoServico.Outro
    ];

    [RelayCommand]
    private void EscolherServico(OpcaoServicoViewModel? opcao)
    {
        if (opcao is null)
        {
            return;
        }

        foreach (var item in Opcoes)
        {
            item.Escolhido = ReferenceEquals(item, opcao);
        }

        Escolha = opcao;
        Erro = null;
    }

    /// <summary>Define por onde a janela vai passar, conforme o que o serviço exige.</summary>
    private void MontarCaminho(FluxoServico fluxo)
    {
        _caminho = [PassoServico.Servico];

        if (fluxo.PedeVeiculo()) _caminho.Add(PassoServico.Veiculo);
        if (fluxo.PedeMulta()) _caminho.Add(PassoServico.Multas);
        if (fluxo.PedeProcessoCnh()) _caminho.Add(PassoServico.Processo);

        _caminho.Add(PassoServico.Preco);
        _caminho.Add(PassoServico.Pagamento);

        Passos.Clear();
        for (var i = 1; i < _caminho.Count; i++)
        {
            Passos.Add(new PassoViewModel((i + 1).ToString(), NomeDoPasso(_caminho[i])));
        }

        // O primeiro passo já está cumprido quando a trilha aparece.
        Passos.Insert(0, new PassoViewModel("1", "Serviço") { Concluido = true });
    }

    private static string NomeDoPasso(PassoServico passo) => passo switch
    {
        PassoServico.Veiculo => "Veículo",
        PassoServico.Multas => "Multas",
        PassoServico.Processo => "Processo",
        PassoServico.Preco => "Preço",
        _ => "Pagamento"
    };

    // ===================== veículo =====================

    [RelayCommand]
    private void SelecionarVeiculo(VeiculoDoServicoViewModel? veiculo)
    {
        if (veiculo is null)
        {
            return;
        }

        foreach (var item in Veiculos)
        {
            item.Escolhido = ReferenceEquals(item, veiculo);
        }

        Veiculo = veiculo;
        Erro = null;
    }

    [RelayCommand]
    private void NovoVeiculo() =>
        SubFormulario = new VeiculoFormularioViewModel(
            _clienteId,
            aoCancelar: () => SubFormulario = null,
            aoSalvar: AposNovoVeiculo);

    private async void AposNovoVeiculo(int veiculoId)
    {
        SubFormulario = null;
        await CarregarVeiculos();

        SelecionarVeiculo(Veiculos.FirstOrDefault(v => v.Id == veiculoId));
    }

    private async Task CarregarVeiculos()
    {
        var escolhido = Veiculo?.Id;
        var doCliente = await _fichas.ListarVeiculosAsync(_clienteId);

        Veiculos.Clear();
        foreach (var veiculo in doCliente)
        {
            Veiculos.Add(new VeiculoDoServicoViewModel(veiculo));
        }

        Veiculo = null;
        if (escolhido is { } id)
        {
            SelecionarVeiculo(Veiculos.FirstOrDefault(v => v.Id == id));
        }

        OnPropertyChanged(nameof(SemVeiculos));
    }

    // ===================== multas =====================

    /// <summary>Registra a multa sem sair do passo em que ela está escolhendo os autos.</summary>
    [RelayCommand]
    private void NovaMulta()
    {
        if (Veiculo is null)
        {
            return;
        }

        SubFormulario = new MultaFormularioViewModel(
            Veiculo.Id,
            Veiculo.Placa,
            aoCancelar: () => SubFormulario = null,
            aoSalvar: AposNovaMulta);
    }

    private async void AposNovaMulta(int multaId)
    {
        SubFormulario = null;
        await CarregarMultas();

        if (Multas.FirstOrDefault(m => m.Id == multaId) is { } nova)
        {
            nova.Marcada = true;
        }
    }

    [RelayCommand]
    private void Alternar(MultaDoServicoViewModel? multa)
    {
        if (multa?.PodeMarcar == true)
        {
            multa.Marcada = !multa.Marcada;
        }
    }

    [RelayCommand]
    private void AlternarPontuacao(MultaDaPontuacaoViewModel? multa)
    {
        if (multa is not null)
        {
            multa.Marcada = !multa.Marcada;
        }
    }

    /// <summary>Registra uma multa da pontuação que ainda não estava no sistema.</summary>
    /// <remarks>
    /// Aqui não há carro escolhido — a pontuação soma autos de qualquer veículo —, então
    /// o formulário é o que pergunta de qual deles é o auto.
    /// </remarks>
    [RelayCommand]
    private void NovaMultaDaPontuacao() =>
        SubFormulario = new MultaFormularioViewModel(
            _clienteId,
            aoCancelar: () => SubFormulario = null,
            aoSalvar: AposMultaDaPontuacao);

    private async void AposMultaDaPontuacao(int multaId)
    {
        SubFormulario = null;

        // Recarrega do zero: a multa nova pode ser de qualquer carro do cliente.
        var marcadas = MultasDoCliente.Where(m => m.Marcada).Select(m => m.Id).ToHashSet();
        MultasDoCliente.Clear();
        await CarregarMultasDoCliente();

        foreach (var multa in MultasDoCliente)
        {
            multa.Marcada = multa.Id == multaId || marcadas.Contains(multa.Id);
        }
    }

    private async Task CarregarMultas()
    {
        if (Veiculo is null)
        {
            return;
        }

        var marcadas = Multas.Where(m => m.Marcada).Select(m => m.Id).ToHashSet();
        Carregando = true;

        try
        {
            var doVeiculo = await _multas.ListarParaRecursoAsync(Veiculo.Id);

            Multas.Clear();
            foreach (var multa in doVeiculo)
            {
                Multas.Add(new MultaDoServicoViewModel(multa, Contar)
                {
                    Marcada = marcadas.Contains(multa.Id) && !multa.JaTemRecurso
                });
            }
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível buscar as multas: {excecao.Message}";
        }
        finally
        {
            Carregando = false;
            Contar();
            OnPropertyChanged(nameof(SemMultas));
        }
    }

    /// <summary>Todas as multas do cliente, de qualquer carro: a pontuação não olha placa.</summary>
    private async Task CarregarMultasDoCliente()
    {
        if (MultasDoCliente.Count > 0)
        {
            return;
        }

        Carregando = true;

        try
        {
            var todas = await _multas.ListarPorClienteAsync(_clienteId);

            MultasDoCliente.Clear();
            foreach (var multa in todas)
            {
                MultasDoCliente.Add(new MultaDaPontuacaoViewModel(multa, ContarPontos));
            }
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível buscar as multas do cliente: {excecao.Message}";
        }
        finally
        {
            Carregando = false;
            ContarPontos();
            OnPropertyChanged(nameof(SemMultasDoCliente));
        }
    }

    private void Contar()
    {
        OnPropertyChanged(nameof(Marcadas));
        OnPropertyChanged(nameof(ResumoMarcadas));
    }

    private void ContarPontos()
    {
        OnPropertyChanged(nameof(PontosMarcados));
        OnPropertyChanged(nameof(ResumoPontuacao));
    }

    // ===================== preço =====================

    private void MontarItens()
    {
        var fluxo = Escolha!.Fluxo;
        var variantes = Escolha.Variantes;

        Itens.Clear();

        if (fluxo.PedeMulta())
        {
            foreach (var multa in Multas.Where(m => m.Marcada))
            {
                Itens.Add(new ItemDoServicoViewModel(
                    IdentificarMulta(multa),
                    multa.Gravidade,
                    multa.FundoGravidade,
                    multa.CorGravidade,
                    variantes,
                    SugerirPelaGravidade(multa.Dados.Gravidade, variantes),
                    multa.Id,
                    multa.Dados.PrazoDefesa,
                    Recalcular));
            }
        }
        else if (fluxo.PedeProcessoCnh())
        {
            Itens.Add(new ItemDoServicoViewModel(
                $"processo {NumeroProcesso.Trim().ToUpperInvariant()}",
                $"{TipoDeProcesso?.ToLowerInvariant()} · {PontosMarcados} pontos",
                "RoxoClaro",
                "Roxo",
                variantes,
                SugerirPeloTipo(variantes),
                null,
                Formato.LerData(PrazoDoProcesso),
                Recalcular));
        }
        else
        {
            Itens.Add(new ItemDoServicoViewModel(
                Veiculo is null ? "da tabela de preços" : $"placa {Veiculo.Placa}",
                string.Empty,
                "Fundo",
                "TextoFraco",
                variantes,
                variantes.Count == 1 ? variantes[0] : null,
                null,
                null,
                Recalcular));
        }

        Recalcular();
    }

    /// <summary>O recurso que combina com a gravidade do auto.</summary>
    private static TipoServico? SugerirPelaGravidade(
        GravidadeMulta? gravidade, IReadOnlyList<TipoServico> variantes)
    {
        var procurado = gravidade switch
        {
            GravidadeMulta.Leve => "recurso de multa leve",
            GravidadeMulta.Media => "recurso de multa media",
            GravidadeMulta.Grave => "recurso de multa grave",
            GravidadeMulta.Gravissima => "recurso de multa gravissima",
            _ => null
        };

        return procurado is null
            ? variantes.Count == 1 ? variantes[0] : null
            : variantes.FirstOrDefault(s => s.Nome.Equals(procurado, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>A defesa que combina com o tipo do processo. Sem indicação é o caso comum.</summary>
    private TipoServico? SugerirPeloTipo(IReadOnlyList<TipoServico> variantes)
    {
        var palavra = TipoDeProcesso == "Cassação" ? "cassacao" : "suspensao";

        return variantes.FirstOrDefault(s =>
                   s.Nome.Contains(palavra, StringComparison.OrdinalIgnoreCase)
                   && s.Nome.Contains("sem indicacao", StringComparison.OrdinalIgnoreCase))
               ?? variantes.FirstOrDefault(s =>
                   s.Nome.Contains(palavra, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Refaz a soma e, enquanto ela não negociou, mantém o final igual à tabela.</summary>
    private void Recalcular()
    {
        OnPropertyChanged(nameof(TotalTabela));
        OnPropertyChanged(nameof(TotalTabelaTexto));
        AtualizarDinheiro();
    }

    private void AtualizarDinheiro()
    {
        OnPropertyChanged(nameof(ValorServicos));
        OnPropertyChanged(nameof(ValorServicosTexto));
        OnPropertyChanged(nameof(ValorFinal));
        OnPropertyChanged(nameof(ValorFinalTotalTexto));
        OnPropertyChanged(nameof(Desconto));
        OnPropertyChanged(nameof(DescontoResumo));
        OnPropertyChanged(nameof(TemDesconto));
        OnPropertyChanged(nameof(ResumoFinal));
    }

    // ===================== navegação =====================

    [RelayCommand]
    private void Voltar()
    {
        Erro = null;

        var atual = _caminho.IndexOf(Passo);
        if (atual > 0)
        {
            IrPara(_caminho[atual - 1]);
        }
    }

    [RelayCommand]
    private async Task Avancar()
    {
        Erro = null;

        if (!await Conferir())
        {
            return;
        }

        if (Passo == PassoServico.Pagamento)
        {
            await Salvar();
            return;
        }

        if (Passo == PassoServico.Servico)
        {
            MontarCaminho(Escolha!.Fluxo);
        }

        var proximo = _caminho[_caminho.IndexOf(Passo) + 1];

        if (proximo == PassoServico.Preco) MontarItens();
        if (proximo == PassoServico.Pagamento) ValorPagoTexto = ValorFinal.ToString("N2", Brasil);

        IrPara(proximo);

        if (proximo == PassoServico.Veiculo) await CarregarVeiculos();
        if (proximo == PassoServico.Multas) await CarregarMultas();
        if (proximo == PassoServico.Processo) await CarregarMultasDoCliente();
    }

    /// <summary>O que falta no passo atual para poder seguir.</summary>
    private async Task<bool> Conferir()
    {
        switch (Passo)
        {
            case PassoServico.Servico when Escolha is null:
                Erro = "Escolha o serviço.";
                return false;

            case PassoServico.Veiculo when Veiculo is null:
                Erro = "Escolha o veículo.";
                return false;

            case PassoServico.Multas when Marcadas == 0:
                Erro = "Marque pelo menos uma multa.";
                return false;

            case PassoServico.Processo:
                return await ConferirProcesso();

            case PassoServico.Preco when Itens.Any(i => i.Servico is null):
                Erro = "Escolha o serviço de cada item.";
                return false;

            case PassoServico.Preco when Formato.LerDinheiro(DescontoTexto) is { } d && d < 0:
                Erro = "Valor combinado inválido.";
                return false;

            default:
                return true;
        }
    }

    private async Task<bool> ConferirProcesso()
    {
        ErroNumero = ErroPrazo = null;

        var numero = NumeroProcesso.Trim();

        if (numero.Length == 0)
        {
            ErroNumero = "O número do processo é obrigatório.";
        }
        else if (await _processos.NumeroJaCadastradoAsync(numero))
        {
            ErroNumero = "Já existe um processo com este número.";
        }

        if (Formato.LerData(PrazoDoProcesso) is null)
        {
            ErroPrazo = "Informe o prazo de defesa. Use dd/mm/aaaa.";
        }

        if (ErroNumero is not null || ErroPrazo is not null)
        {
            Erro = "Confira os campos marcados.";
            return false;
        }

        return true;
    }

    private async Task Salvar()
    {
        if (Salvando)
        {
            return;
        }

        var valorFinal = ValorFinal;
        decimal pago = 0m;

        if (PagarAgora)
        {
            if (Formato.LerDinheiro(ValorPagoTexto) is not { } valor || valor <= 0)
            {
                Erro = "Informe o valor pago ou selecione \"Deixar em aberto\".";
                return;
            }

            if (valor > valorFinal)
            {
                Erro = "O valor pago não pode exceder o valor combinado.";
                return;
            }

            pago = valor;
        }

        Salvando = true;

        try
        {
            await _contratacoes.CadastrarAsync(
                Montar(valorFinal),
                PagarAgora ? Pagar(pago) : null,
                Escolha!.Fluxo.PedeProcessoCnh() ? MontarProcesso() : null,
                Despesas.Select(d => d.Montar()).ToList());

            _aoSalvar?.Invoke();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível contratar: {excecao.Message}";
        }
        finally
        {
            Salvando = false;
        }
    }

    private Contratacao Montar(decimal valorFinal) => new()
    {
        ClienteId = _clienteId,
        DataContratacao = DateTime.Today,
        ValorTabela = TotalTabela,
        Desconto = Desconto,
        ValorFinal = valorFinal,
        Situacao = SituacaoRegistro.Andamento,
        Observacoes = Limpar(JuntarObservacoes()),
        Itens = Itens.Select(item => new ItemContratacao
        {
            TipoServicoId = item.Servico!.Id,
            MultaId = item.MultaId,
            Situacao = SituacaoRegistro.Andamento,

            // Serviço com fases começa na autuação, com o prazo que veio do auto ou da
            // notificação. Serviço direto não tem fase: ele anda pelo roteiro de etapas,
            // que o DAO cria a partir do modelo.
            Instancias = item.Servico.TemInstancias
                ?
                [
                    new Instancia
                    {
                        Fase = FaseInstancia.Autuacao,
                        Situacao = SituacaoInstancia.Contratado,
                        PrazoProtocolo = item.Prazo
                    }
                ]
                : []
        }).ToList()
    };

    private ProcessoCnh MontarProcesso() => new()
    {
        ClienteId = _clienteId,
        NumeroProcesso = NumeroProcesso.Trim().ToUpperInvariant(),
        Tipo = TipoDeProcesso == "Cassação" ? TipoProcessoCnh.Cassacao : TipoProcessoCnh.Suspensao,
        Motivo = PontosMarcados > 0 ? $"Pontuação: {PontosMarcados} pontos" : null,
        DataNotificacao = Formato.LerData(DataNotificacao),
        PrazoDefesa = Formato.LerData(PrazoDoProcesso),
        Portaria = Limpar(Portaria),
        OrgaoInstaurador = Limpar(OrgaoInstaurador),
        MultasRelacionadas = MultasDoCliente.Where(m => m.Marcada).Select(m => m.Dados).ToList()
    };

    /// <summary>O motivo do desconto entra nas observações: é lá que ela relê depois.</summary>
    private string JuntarObservacoes()
    {
        var anotado = Observacoes.Trim();
        var motivo = MotivoDesconto.Trim();

        if (motivo.Length == 0 || Desconto <= 0)
        {
            return anotado;
        }

        var linha = $"Desconto de {Formato.Dinheiro(Desconto)}: {motivo}";
        return anotado.Length == 0 ? linha : anotado + Environment.NewLine + linha;
    }

    private string IdentificarMulta(MultaDoServicoViewModel multa) =>
        Veiculo is null ? $"AIT {multa.Ait}" : $"AIT {multa.Ait} · {Veiculo.Placa}";

    private Pagamento Pagar(decimal valor) => new()
    {
        DataPagamento = DateTime.Today,
        Valor = valor,
        Forma = FormaEscolhida(),
        Parcelas = Parcelavel && int.TryParse(Parcelas, out var n) && n is >= 1 and <= 12 ? n : 1,
        Observacao = Limpar(ObservacaoPagamento)
    };

    private FormaPagamento FormaEscolhida() => Forma switch
    {
        "Dinheiro" => FormaPagamento.Dinheiro,
        "PIX parcelado" => FormaPagamento.PixParcelado,
        "Débito" => FormaPagamento.Debito,
        "Crédito" => FormaPagamento.Credito,
        _ => FormaPagamento.Pix
    };

    private static string? Limpar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private void IrPara(PassoServico passo)
    {
        Passo = passo;

        var posicao = _caminho.IndexOf(passo);
        for (var i = 0; i < Passos.Count; i++)
        {
            Passos[i].Atual = i == posicao;
            Passos[i].Concluido = i < posicao;
        }
    }

    partial void OnPassoChanged(PassoServico valor)
    {
        OnPropertyChanged(nameof(NoServico));
        OnPropertyChanged(nameof(NoVeiculo));
        OnPropertyChanged(nameof(NasMultas));
        OnPropertyChanged(nameof(NoProcesso));
        OnPropertyChanged(nameof(NoPreco));
        OnPropertyChanged(nameof(NoPagamento));
        OnPropertyChanged(nameof(Instrucao));
        OnPropertyChanged(nameof(Contador));
        OnPropertyChanged(nameof(PodeVoltar));
        OnPropertyChanged(nameof(TextoAvancar));
        OnPropertyChanged(nameof(MostrarTrilha));
    }

    partial void OnEscolhaChanged(OpcaoServicoViewModel? valor)
    {
        OnPropertyChanged(nameof(NomeDoServico));
        OnPropertyChanged(nameof(TemEscolha));
    }

    partial void OnVeiculoChanged(VeiculoDoServicoViewModel? valor)
    {
        OnPropertyChanged(nameof(PlacaEscolhida));
        OnPropertyChanged(nameof(TemVeiculo));
    }

    partial void OnBuscaServicoChanged(string valor) => MontarOpcoes();

    partial void OnDescontoTextoChanged(string valor) => AtualizarDinheiro();

    partial void OnErroChanged(string? valor) => OnPropertyChanged(nameof(TemErro));

    partial void OnCarregandoChanged(bool valor)
    {
        OnPropertyChanged(nameof(SemVeiculos));
        OnPropertyChanged(nameof(SemMultas));
        OnPropertyChanged(nameof(SemMultasDoCliente));
    }

    partial void OnFormaChanged(string? valor) => OnPropertyChanged(nameof(Parcelavel));
}
