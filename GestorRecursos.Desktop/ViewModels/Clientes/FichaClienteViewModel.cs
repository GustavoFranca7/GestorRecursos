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
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>As abas da ficha.</summary>
public enum AbaFicha
{
    Dados,
    Veiculos,
    Processos,
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
    private readonly Action? _aoVoltar;
    private readonly Action? _aoNovoCliente;
    private readonly Action<GrupoCadastro>? _aoEditar;

    /// <summary>Confirmação da lixeira do cabeçalho.</summary>
    [ObservableProperty]
    public partial bool ArquivamentoAberto { get; set; }

    /// <summary>Quantos vizinhos já vieram do banco; a lista cresce conforme rola.</summary>
    private const int PorLote = 25;
    private bool _carregandoVizinhos;
    private int _paginaVizinhos;
    private int _totalVizinhos;

    public ObservableCollection<AbaFichaViewModel> Abas { get; } =
    [
        new("Dados", AbaFicha.Dados, pronta: true),
        new("Veículos e multas", AbaFicha.Veiculos, pronta: false),
        new("Processos", AbaFicha.Processos, pronta: false),
        new("Financeiro", AbaFicha.Financeiro, pronta: false),
        new("Documentos", AbaFicha.Documentos, pronta: false),
        new("Histórico", AbaFicha.Historico, pronta: false)
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
    public bool AbaDados => AbaAtual == AbaFicha.Dados;
    public bool AbaEmConstrucao => AbaAtual != AbaFicha.Dados;

    public FichaClienteViewModel()
    {
        // Construtor sem argumentos só para o preview do Rider.
    }

    public FichaClienteViewModel(
        int clienteId,
        Action? aoVoltar = null,
        Action? aoNovoCliente = null,
        Action<GrupoCadastro>? aoEditar = null)
    {
        ClienteId = clienteId;
        _aoVoltar = aoVoltar;
        _aoNovoCliente = aoNovoCliente;
        _aoEditar = aoEditar;

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

        try
        {
            var ficha = await _fichas.BuscarAsync(ClienteId);
            if (ficha is null)
            {
                Erro = "Cliente não encontrado. Ele pode ter sido apagado em outra tela.";
                return;
            }

            Preencher(ficha);
            await CarregarVizinhos();
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
    private void TrocarAba(AbaFicha aba)
    {
        AbaAtual = aba;

        foreach (var item in Abas)
        {
            item.Escolhida = item.Aba == aba;
        }
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
        TrocarAba(AbaFicha.Dados);
        await Carregar();
    }

    [RelayCommand]
    private Task FiltrarLista() => CarregarVizinhos();

    private void Preencher(FichaCliente ficha)
    {
        var cliente = ficha.Cliente;

        Nome = cliente.Nome;
        Identificacao = MontarIdentificacao(cliente);

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

        DadosCnh.Clear();
        DadosCnh.Add(new CampoFicha("Número", cliente.CnhNumero));
        DadosCnh.Add(new CampoFicha("Categoria", cliente.CnhCategoria));
        DadosCnh.Add(new CampoFicha("Validade", Formato.Data(cliente.CnhValidade)));
        DadosCnh.Add(new CampoFicha("Registro", cliente.CnhRegistro));

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
        AchaAba(AbaFicha.Processos).Contador = ficha.Processos.Count;
        TrocarAba(AbaAtual);

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
            var nome = grupo.Key switch
            {
                "AUTUACAO" => "autuação",
                "JARI" => "JARI",
                "CETRAN" => "CETRAN",
                _ => grupo.Key
            };

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

    /// <summary>A lixeira do cabeçalho pergunta antes: arquivar tira o cliente das listas.</summary>
    [RelayCommand]
    private void PedirArquivamento() => ArquivamentoAberto = true;

    [RelayCommand]
    private void CancelarArquivamento() => ArquivamentoAberto = false;

    [RelayCommand]
    private async Task ConfirmarArquivamento()
    {
        try
        {
            await _clientes.ArquivarAsync(ClienteId);
            ArquivamentoAberto = false;

            // A ficha deixa de fazer sentido aqui: o cliente agora vive em Arquivados.
            _aoVoltar?.Invoke();
        }
        catch (Exception excecao)
        {
            ArquivamentoAberto = false;
            Erro = $"Não foi possível arquivar: {excecao.Message}";
        }
    }

    private AbaFichaViewModel AchaAba(AbaFicha aba) => Abas.First(a => a.Aba == aba);

    partial void OnAbaAtualChanged(AbaFicha valor)
    {
        OnPropertyChanged(nameof(AbaDados));
        OnPropertyChanged(nameof(AbaEmConstrucao));
    }

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

    public string Fase { get; } = dados.Fase switch
    {
        "AUTUACAO" => "autuação",
        "JARI" => "JARI",
        "CETRAN" => "CETRAN",
        _ => dados.Fase
    };

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

    private static string TextoDaSituacao(string situacao) => situacao switch
    {
        "CONTRATADO" => "contratado",
        "AGUARDANDO_DOCUMENTO" => "aguardando documento",
        "AGUARDANDO_PROTOCOLO_JARI" => "aguardando protocolo na JARI",
        "AGUARDANDO_PROTOCOLO_CETRAN" => "aguardando protocolo no CETRAN",
        "PROTOCOLADO" => "protocolado",
        "AGUARDANDO_JULGAMENTO" => "aguardando julgamento",
        "PRAZO_PERDIDO" => "prazo perdido",
        _ => situacao.ToLowerInvariant().Replace('_', ' ')
    };
}
