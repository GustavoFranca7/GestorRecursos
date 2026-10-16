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

/// <summary>Em que etapa o recurso está sendo montado.</summary>
public enum PassoRecurso
{
    Veiculo,
    Multas,
    Servico,
    Pagamento
}

/// <summary>Um veículo do cliente, na escolha do passo 1.</summary>
public partial class VeiculoDoRecursoViewModel(VeiculoDaFicha dados) : ObservableObject
{
    /// <summary>Linha marcada. Mora no item porque é ele que muda de cor.</summary>
    [ObservableProperty] public partial bool Escolhido { get; set; }

    public int Id { get; } = dados.Id;
    public string Placa { get; } = dados.Placa;

    public string Descricao { get; } = string.Join(" · ",
        new[] { dados.MarcaModelo, dados.Ano, dados.Cor }
            .Where(p => !string.IsNullOrWhiteSpace(p)));

    public string Vinculo { get; } = Rotulos.Vinculo(dados.Vinculo);

    public string Multas { get; } = dados.QuantidadeMultas == 1
        ? "1 multa"
        : $"{dados.QuantidadeMultas} multas";
}

/// <summary>Uma multa do veículo, na seleção do passo 2.</summary>
public partial class MultaDoRecursoViewModel : ObservableObject
{
    private readonly Action _aoMarcar;

    public MultaDoRecursoViewModel(MultaParaRecurso dados, Action aoMarcar)
    {
        Dados = dados;
        _aoMarcar = aoMarcar;

        var gravidade = dados.Gravidade?.Texto();
        var (fundo, texto) = Rotulos.CoresDaGravidade(gravidade);
        Gravidade = Rotulos.Gravidade(gravidade, null);
        FundoGravidade = fundo;
        CorGravidade = texto;

        Descricao = string.IsNullOrWhiteSpace(dados.Descricao)
            ? dados.Enquadramento ?? "sem descrição"
            : dados.Descricao;

        Quando = dados.DataHoraInfracao is { } momento ? Formato.Data(momento) : "sem data";
        Valor = dados.Valor is { } valor ? Formato.Dinheiro(valor) : "—";

        // O prazo de defesa é o que decide a urgência do recurso; sem ele, não há alarme.
        if (dados.PrazoDefesa is { } prazo)
        {
            var dias = (prazo.Date - DateTime.Today).Days;
            Prazo = Prazos.Texto(dias);
            var (fundoPrazo, corPrazo) = Prazos.Cores(dias);
            FundoPrazo = fundoPrazo;
            CorPrazo = corPrazo;
        }
        else
        {
            Prazo = "sem prazo";
            FundoPrazo = "Fundo";
            CorPrazo = "TextoFraco";
        }
    }

    public MultaParaRecurso Dados { get; }

    /// <summary>Entra neste recurso. Duas vias: o clique na linha e o quadradinho.</summary>
    [ObservableProperty] public partial bool Marcada { get; set; }

    public int Id => Dados.Id;
    public string Ait => Dados.Ait;
    public string Descricao { get; }
    public string Quando { get; }
    public string Gravidade { get; }
    public string FundoGravidade { get; }
    public string CorGravidade { get; }
    public string Valor { get; }
    public string Prazo { get; }
    public string FundoPrazo { get; }
    public string CorPrazo { get; }

    public bool JaTemRecurso => Dados.JaTemRecurso;
    public bool PodeMarcar => !Dados.JaTemRecurso;
    public bool TemGravidade => Gravidade.Length > 0;

    partial void OnMarcadaChanged(bool valor) => _aoMarcar();
}

/// <summary>Uma multa escolhida, com o serviço e o preço dela, no passo 3.</summary>
/// <remarks>
/// O serviço vem sugerido pela gravidade, que é como a tabela do escritório é organizada.
/// Continua trocável: o mesmo auto pode virar indicação de condutor em vez de recurso.
/// </remarks>
public partial class ItemDoRecursoViewModel : ObservableObject
{
    private readonly Action _aoTrocar;

    public ItemDoRecursoViewModel(
        MultaDoRecursoViewModel multa,
        IReadOnlyList<TipoServico> servicos,
        Action aoTrocar)
    {
        _aoTrocar = aoTrocar;
        MultaId = multa.Id;
        Ait = multa.Ait;
        PrazoDefesa = multa.Dados.PrazoDefesa;
        Servicos = servicos;

        Resumo = string.Join(" · ",
            new[] { multa.Gravidade, multa.Dados.Enquadramento }
                .Where(p => !string.IsNullOrWhiteSpace(p)));

        Servico = Sugerir(multa.Dados.Gravidade, servicos);
    }

    public int MultaId { get; }
    public string Ait { get; }
    public string Resumo { get; }
    public DateTime? PrazoDefesa { get; }
    public IReadOnlyList<TipoServico> Servicos { get; }

    [ObservableProperty] public partial TipoServico? Servico { get; set; }

    public decimal Valor => Servico?.ValorTabela ?? 0m;
    public string ValorTexto => Servico is null ? "—" : Formato.Dinheiro(Valor);

    /// <summary>O serviço da tabela que combina com a gravidade do auto.</summary>
    private static TipoServico? Sugerir(GravidadeMulta? gravidade, IReadOnlyList<TipoServico> servicos)
    {
        var procurado = gravidade switch
        {
            GravidadeMulta.Leve => "recurso de multa leve",
            GravidadeMulta.Media => "recurso de multa media",
            GravidadeMulta.Grave => "recurso de multa grave",
            GravidadeMulta.Gravissima => "recurso de multa gravissima",
            _ => null
        };

        if (procurado is null)
        {
            return null;
        }

        return servicos.FirstOrDefault(s => s.Nome.Equals(procurado, StringComparison.OrdinalIgnoreCase));
    }

    partial void OnServicoChanged(TipoServico? valor)
    {
        OnPropertyChanged(nameof(ValorTexto));
        _aoTrocar();
    }
}

/// <summary>Monta um recurso do começo ao fim: veículo, multas, preço e pagamento.</summary>
/// <remarks>
/// É a ação do dia a dia do escritório. Ninguém abre o sistema para cadastrar uma multa:
/// abre para fechar um recurso, e a multa é um passo do caminho. Por isso a janela cria o
/// que faltar sem obrigar a trocar de aba.
///
/// Veículo e multa criados aqui são gravados na hora, pelos formulários de sempre: eles
/// existem no mundo real mesmo que o negócio não feche. Só o que é dinheiro — contratação,
/// itens, instâncias e pagamento — espera o fim e vai numa transação só.
/// </remarks>
public partial class NovoRecursoViewModel : ViewModelBase
{
    private static readonly CultureInfo Brasil = CultureInfo.GetCultureInfo("pt-BR");

    private readonly FichaClienteDAO _fichas = new();
    private readonly MultaDAO _multas = new();
    private readonly TipoServicoDAO _tiposServico = new();
    private readonly ContratacaoDAO _contratacoes = new();

    private readonly int _clienteId;
    private readonly Action? _aoCancelar;
    private readonly Action? _aoSalvar;

    private IReadOnlyList<TipoServico> _tabela = [];

    /// <summary>Evita que o recálculo do preço apague o valor que ela acabou de digitar.</summary>
    private bool _escrevendoValor;
    private bool _valorNegociado;

    public NovoRecursoViewModel(int clienteId, Action? aoCancelar = null, Action? aoSalvar = null)
    {
        _clienteId = clienteId;
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;

        IrPara(PassoRecurso.Veiculo);

        if (!Design.IsDesignMode)
        {
            CarregarCommand.Execute(null);
        }
    }

    public ObservableCollection<PassoViewModel> Passos { get; } =
    [
        new("1", "Veículo"),
        new("2", "Multas"),
        new("3", "Serviço"),
        new("4", "Pagamento")
    ];

    public ObservableCollection<VeiculoDoRecursoViewModel> Veiculos { get; } = [];
    public ObservableCollection<MultaDoRecursoViewModel> Multas { get; } = [];
    public ObservableCollection<ItemDoRecursoViewModel> Itens { get; } = [];

    public IReadOnlyList<string> Formas { get; } =
        ["Dinheiro", "PIX", "PIX parcelado", "Débito", "Crédito"];

    // ---------- estado da janela ----------
    [ObservableProperty] public partial PassoRecurso Passo { get; set; } = PassoRecurso.Veiculo;
    [ObservableProperty] public partial bool Carregando { get; set; }
    [ObservableProperty] public partial bool Salvando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    /// <summary>Cadastro de veículo ou de multa aberto por cima desta janela.</summary>
    [ObservableProperty] public partial ViewModelBase? SubFormulario { get; set; }

    // ---------- passo 1 ----------
    [ObservableProperty] public partial VeiculoDoRecursoViewModel? Veiculo { get; set; }

    // ---------- passo 3 ----------
    [ObservableProperty] public partial string ValorFinalTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string Observacoes { get; set; } = string.Empty;

    // ---------- passo 4 ----------
    [ObservableProperty] public partial bool PagarAgora { get; set; } = true;
    [ObservableProperty] public partial string? Forma { get; set; } = "PIX";
    [ObservableProperty] public partial string ValorPagoTexto { get; set; } = string.Empty;
    [ObservableProperty] public partial string Parcelas { get; set; } = "1";
    [ObservableProperty] public partial string ObservacaoPagamento { get; set; } = string.Empty;

    public bool NoVeiculo => Passo == PassoRecurso.Veiculo;
    public bool NasMultas => Passo == PassoRecurso.Multas;
    public bool NoServico => Passo == PassoRecurso.Servico;
    public bool NoPagamento => Passo == PassoRecurso.Pagamento;

    public bool TemErro => Erro is not null;
    public bool SemVeiculos => !Carregando && Veiculos.Count == 0;
    public bool SemMultas => !Carregando && Multas.Count == 0;

    public string Titulo => "Novo recurso";

    public string Instrucao => Passo switch
    {
        PassoRecurso.Veiculo => "De qual carro é o auto de infração?",
        PassoRecurso.Multas => "Marque as multas que entram neste recurso.",
        PassoRecurso.Servico => "Confira o serviço de cada multa e feche o valor.",
        _ => "Como o cliente vai pagar?"
    };

    public string Contador => $"Passo {(int)Passo + 1} de 4";
    public bool PodeVoltar => Passo != PassoRecurso.Veiculo;

    public string TextoAvancar => Passo == PassoRecurso.Pagamento ? "Criar recurso" : "Avançar →";

    /// <summary>A placa escolhida, repetida no topo dos passos seguintes.</summary>
    public string PlacaEscolhida => Veiculo?.Placa ?? string.Empty;
    public bool TemVeiculo => Veiculo is not null;

    public int Marcadas => Multas.Count(m => m.Marcada);

    public string ResumoMarcadas => Marcadas switch
    {
        0 => "nenhuma multa marcada",
        1 => "1 multa marcada",
        _ => $"{Marcadas} multas marcadas"
    };

    public decimal TotalTabela => Itens.Sum(i => i.Valor);
    public string TotalTabelaTexto => Formato.Dinheiro(TotalTabela);

    public decimal ValorFinal => Formato.LerDinheiro(ValorFinalTexto) ?? 0m;

    /// <summary>O que ela abriu mão. Sai da conta, não de um campo: campo desatualiza.</summary>
    public decimal Desconto => Math.Max(0m, TotalTabela - ValorFinal);
    public string DescontoTexto => Formato.Dinheiro(Desconto);
    public bool TemDesconto => Desconto > 0;

    /// <summary>Parcelas só fazem sentido onde o cliente parcela.</summary>
    public bool Parcelavel => Forma is "Crédito" or "PIX parcelado";

    public string ResumoFinal =>
        $"{ResumoMarcadas} · {Formato.Dinheiro(ValorFinal)}";

    [RelayCommand]
    private void Cancelar() => _aoCancelar?.Invoke();

    [RelayCommand]
    private async Task Carregar()
    {
        Carregando = true;
        Erro = null;

        try
        {
            // A tabela de preços vem junto na abertura: ela é pequena e o passo 3
            // precisaria dela de qualquer forma, sem ter o que mostrar enquanto espera.
            _tabela = await _tiposServico.ListarAsync();
            await CarregarVeiculos();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível abrir o recurso: {excecao.Message}";
        }
        finally
        {
            Carregando = false;
            OnPropertyChanged(nameof(SemVeiculos));
        }
    }

    // ===================== passo 1: veículo =====================

    [RelayCommand]
    private void SelecionarVeiculo(VeiculoDoRecursoViewModel? veiculo)
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

    /// <summary>Abre o assistente de veículo de sempre, por cima deste.</summary>
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

        // Quem acabou de cadastrar o carro quer seguir com ele: já vem escolhido.
        SelecionarVeiculo(Veiculos.FirstOrDefault(v => v.Id == veiculoId));
    }

    private async Task CarregarVeiculos()
    {
        var escolhido = Veiculo?.Id;
        var doCliente = await _fichas.ListarVeiculosAsync(_clienteId);

        Veiculos.Clear();
        foreach (var veiculo in doCliente)
        {
            Veiculos.Add(new VeiculoDoRecursoViewModel(veiculo));
        }

        Veiculo = null;
        if (escolhido is { } id)
        {
            SelecionarVeiculo(Veiculos.FirstOrDefault(v => v.Id == id));
        }

        OnPropertyChanged(nameof(SemVeiculos));
    }

    // ===================== passo 2: multas =====================

    /// <summary>Abre o formulário de multa de sempre, já no veículo escolhido.</summary>
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

        // A multa foi registrada para entrar neste recurso: já vem marcada.
        if (Multas.FirstOrDefault(m => m.Id == multaId) is { } nova)
        {
            nova.Marcada = true;
        }
    }

    [RelayCommand]
    private void Alternar(MultaDoRecursoViewModel? multa)
    {
        if (multa?.PodeMarcar == true)
        {
            multa.Marcada = !multa.Marcada;
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
                var linha = new MultaDoRecursoViewModel(multa, Contar)
                {
                    Marcada = marcadas.Contains(multa.Id) && !multa.JaTemRecurso
                };

                Multas.Add(linha);
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

    private void Contar()
    {
        OnPropertyChanged(nameof(Marcadas));
        OnPropertyChanged(nameof(ResumoMarcadas));
    }

    // ===================== passo 3: serviço e preço =====================

    private void MontarItens()
    {
        Itens.Clear();
        foreach (var multa in Multas.Where(m => m.Marcada))
        {
            Itens.Add(new ItemDoRecursoViewModel(multa, _tabela, Recalcular));
        }

        _valorNegociado = false;
        Recalcular();
    }

    /// <summary>Refaz a soma e, enquanto ela não negociou, mantém o final igual à tabela.</summary>
    private void Recalcular()
    {
        OnPropertyChanged(nameof(TotalTabela));
        OnPropertyChanged(nameof(TotalTabelaTexto));

        if (!_valorNegociado)
        {
            _escrevendoValor = true;
            ValorFinalTexto = TotalTabela.ToString("N2", Brasil);
            _escrevendoValor = false;
        }

        AtualizarDinheiro();
    }

    private void AtualizarDinheiro()
    {
        OnPropertyChanged(nameof(ValorFinal));
        OnPropertyChanged(nameof(Desconto));
        OnPropertyChanged(nameof(DescontoTexto));
        OnPropertyChanged(nameof(TemDesconto));
        OnPropertyChanged(nameof(ResumoFinal));
    }

    // ===================== navegação =====================

    [RelayCommand]
    private void Voltar()
    {
        Erro = null;
        IrPara(Passo - 1);
    }

    [RelayCommand]
    private async Task Avancar()
    {
        Erro = null;

        switch (Passo)
        {
            case PassoRecurso.Veiculo:
                if (Veiculo is null)
                {
                    Erro = "Escolha o veículo do auto de infração.";
                    return;
                }

                IrPara(PassoRecurso.Multas);
                await CarregarMultas();
                return;

            case PassoRecurso.Multas:
                if (Marcadas == 0)
                {
                    Erro = "Marque pelo menos uma multa.";
                    return;
                }

                MontarItens();
                IrPara(PassoRecurso.Servico);
                return;

            case PassoRecurso.Servico:
                if (Itens.Any(i => i.Servico is null))
                {
                    Erro = "Escolha o serviço de cada multa.";
                    return;
                }

                if (Formato.LerDinheiro(ValorFinalTexto) is not { } fechado || fechado < 0)
                {
                    Erro = "Valor combinado inválido.";
                    return;
                }

                // O que o cliente deve nasce quitado por padrão: é o caso comum do balcão.
                ValorPagoTexto = fechado.ToString("N2", Brasil);
                IrPara(PassoRecurso.Pagamento);
                return;

            default:
                await Salvar();
                return;
        }
    }

    private async Task Salvar()
    {
        if (Salvando)
        {
            return;
        }

        var valorFinal = Formato.LerDinheiro(ValorFinalTexto) ?? 0m;
        decimal pago = 0m;

        if (PagarAgora)
        {
            if (Formato.LerDinheiro(ValorPagoTexto) is not { } valor || valor <= 0)
            {
                Erro = "Informe quanto o cliente pagou, ou deixe em aberto.";
                return;
            }

            if (valor > valorFinal)
            {
                Erro = "O pago não pode ser maior que o valor combinado.";
                return;
            }

            pago = valor;
        }

        Salvando = true;

        try
        {
            await _contratacoes.CadastrarAsync(Montar(valorFinal), PagarAgora ? Pagar(pago) : null);
            _aoSalvar?.Invoke();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível criar o recurso: {excecao.Message}";
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
        Desconto = Math.Max(0m, TotalTabela - valorFinal),
        ValorFinal = valorFinal,
        Situacao = SituacaoRegistro.Andamento,
        Observacoes = Limpar(Observacoes),
        Itens = Itens.Select(item => new ItemContratacao
        {
            TipoServicoId = item.Servico!.Id,
            MultaId = item.MultaId,
            Situacao = SituacaoRegistro.Andamento,

            // Recurso começa na autuação, com o prazo de defesa do próprio auto.
            // Serviço sem instâncias (indicação de condutor, por exemplo) não tem fase.
            Instancias = item.Servico.TemInstancias
                ?
                [
                    new Instancia
                    {
                        Fase = FaseInstancia.Autuacao,
                        Situacao = SituacaoInstancia.Contratado,
                        PrazoProtocolo = item.PrazoDefesa
                    }
                ]
                : []
        }).ToList()
    };

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

    private void IrPara(PassoRecurso passo)
    {
        Passo = passo;

        for (var i = 0; i < Passos.Count; i++)
        {
            Passos[i].Atual = i == (int)passo;
            Passos[i].Concluido = i < (int)passo;
        }
    }

    partial void OnPassoChanged(PassoRecurso valor)
    {
        OnPropertyChanged(nameof(NoVeiculo));
        OnPropertyChanged(nameof(NasMultas));
        OnPropertyChanged(nameof(NoServico));
        OnPropertyChanged(nameof(NoPagamento));
        OnPropertyChanged(nameof(Instrucao));
        OnPropertyChanged(nameof(Contador));
        OnPropertyChanged(nameof(PodeVoltar));
        OnPropertyChanged(nameof(TextoAvancar));
    }

    partial void OnVeiculoChanged(VeiculoDoRecursoViewModel? valor)
    {
        OnPropertyChanged(nameof(PlacaEscolhida));
        OnPropertyChanged(nameof(TemVeiculo));
    }

    // Mexer no valor final é negociar: a partir daí a soma da tabela não manda mais nele.
    partial void OnValorFinalTextoChanged(string valor)
    {
        if (!_escrevendoValor)
        {
            _valorNegociado = true;
        }

        AtualizarDinheiro();
    }

    partial void OnErroChanged(string? valor) => OnPropertyChanged(nameof(TemErro));
    partial void OnCarregandoChanged(bool valor)
    {
        OnPropertyChanged(nameof(SemVeiculos));
        OnPropertyChanged(nameof(SemMultas));
    }

    partial void OnFormaChanged(string? valor) => OnPropertyChanged(nameof(Parcelavel));
}
