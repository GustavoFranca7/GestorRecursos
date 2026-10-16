using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;
using GestorRecursos.Core.Validacao;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Em que etapa a janela de veículo está.</summary>
public enum PassoVeiculo
{
    Origem,
    Escolher,
    Dados
}

/// <summary>Uma bolinha da trilha de passos.</summary>
public partial class PassoViewModel(string numero, string titulo) : ObservableObject
{
    public string Numero { get; } = numero;

    [ObservableProperty] public partial string Titulo { get; set; } = titulo;
    [ObservableProperty] public partial bool Atual { get; set; }
    [ObservableProperty] public partial bool Concluido { get; set; }
}

/// <summary>Um veículo do sistema na lista de escolha.</summary>
public partial class VeiculoParaVincularViewModel(VeiculoParaVincular dados) : ObservableObject
{
    /// <summary>Linha marcada. Mora no item porque é ele que muda de cor.</summary>
    [ObservableProperty] public partial bool Escolhido { get; set; }

    public int Id { get; } = dados.Id;
    public string Placa { get; } = dados.Placa;

    public string Descricao { get; } = string.Join(" · ",
        new[] { dados.MarcaModelo, dados.Ano, dados.Cor }
            .Where(p => !string.IsNullOrWhiteSpace(p)));

    public string Renavam { get; } = string.IsNullOrWhiteSpace(dados.Renavam)
        ? "sem RENAVAM"
        : $"RENAVAM {dados.Renavam}";

    /// <summary>"2 multas · 1 cliente": ajuda a reconhecer o carro certo.</summary>
    public string Resumo { get; } =
        $"{Plural(dados.Multas, "multa", "multas")} · {Plural(dados.Clientes, "cliente", "clientes")}";

    public bool JaLigado { get; } = dados.JaLigadoAoCliente;
    public bool PodeLigar => !JaLigado;

    private static string Plural(int quantos, string um, string varios) =>
        quantos == 1 ? $"1 {um}" : $"{quantos} {varios}";
}

/// <summary>Põe um veículo na ficha do cliente, em etapas.</summary>
/// <remarks>
/// O primeiro passo pergunta se o carro já existe no sistema. É o que evita o mesmo
/// veículo cadastrado duas vezes — a placa é única no banco, e descobrir isso só na hora
/// de salvar é tarde demais.
///
/// Editar um veículo já ligado entra direto nos dados, sem passos: não há o que escolher.
/// </remarks>
public partial class VeiculoFormularioViewModel : ViewModelBase
{
    private readonly VeiculoDAO _veiculos = new();
    private readonly Action? _aoCancelar;
    private readonly Action<int>? _aoSalvar;
    private readonly int _clienteId;
    private readonly Veiculo? _original;

    /// <summary>Veículo que já existe com a placa digitada, à espera de confirmação.</summary>
    private Veiculo? _encontrado;

    public VeiculoFormularioViewModel(
        int clienteId,
        Action? aoCancelar = null,
        Action<int>? aoSalvar = null,
        VeiculoComMultas? paraEditar = null)
    {
        _clienteId = clienteId;
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;

        if (paraEditar is null)
        {
            IrPara(PassoVeiculo.Origem);
            return;
        }

        _original = new Veiculo { Id = paraEditar.Id };
        Placa = paraEditar.Placa;
        Renavam = paraEditar.Renavam ?? string.Empty;
        MarcaModelo = paraEditar.MarcaModelo ?? string.Empty;
        Cor = paraEditar.Cor ?? string.Empty;
        Ano = paraEditar.Ano ?? string.Empty;
        Observacao = paraEditar.Observacao ?? string.Empty;
        Vinculo = paraEditar.Vinculo == "CONDUTOR" ? Vinculos[1] : Vinculos[0];

        IrPara(PassoVeiculo.Dados);
    }

    public IReadOnlyList<string> Vinculos { get; } = ["Proprietário", "Condutor indicado"];

    /// <summary>A trilha do topo. O segundo passo muda de nome conforme o caminho.</summary>
    public ObservableCollection<PassoViewModel> Passos { get; } =
    [
        new("1", "Origem"),
        new("2", "Veículo")
    ];

    // ---------- dados do veículo ----------
    [ObservableProperty] public partial string Placa { get; set; } = string.Empty;
    [ObservableProperty] public partial string Renavam { get; set; } = string.Empty;
    [ObservableProperty] public partial string MarcaModelo { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cor { get; set; } = string.Empty;
    [ObservableProperty] public partial string Ano { get; set; } = string.Empty;
    [ObservableProperty] public partial string Observacao { get; set; } = string.Empty;
    [ObservableProperty] public partial string? Vinculo { get; set; } = "Proprietário";

    // ---------- escolha de veículo existente ----------
    public ObservableCollection<VeiculoParaVincularViewModel> Encontrados { get; } = [];

    [ObservableProperty] public partial string Busca { get; set; } = string.Empty;
    [ObservableProperty] public partial VeiculoParaVincularViewModel? Escolhido { get; set; }
    [ObservableProperty] public partial bool Procurando { get; set; }

    // ---------- estado da janela ----------
    [ObservableProperty] public partial PassoVeiculo Passo { get; set; } = PassoVeiculo.Origem;
    [ObservableProperty] public partial bool Salvando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? ErroPlaca { get; set; }

    /// <summary>Aviso de placa já cadastrada, com o convite para ligá-la a este cliente.</summary>
    [ObservableProperty] public partial string? Achado { get; set; }

    public bool Editando => _original is not null;
    public bool NaOrigem => Passo == PassoVeiculo.Origem;
    public bool NaEscolha => Passo == PassoVeiculo.Escolher;
    public bool NosDados => Passo == PassoVeiculo.Dados;

    /// <summary>Editar não é um caminho de várias etapas: a trilha some.</summary>
    public bool MostrarPassos => !Editando;

    public string Titulo => Editando ? "Editar veículo" : "Veículo do cliente";

    public string Instrucao => Passo switch
    {
        PassoVeiculo.Origem => "O carro já está no sistema ou é a primeira vez?",
        PassoVeiculo.Escolher => "Procure pela placa ou pelo RENAVAM.",
        _ => Editando
            ? "A multa pertence ao veículo, então é por ele que se começa."
            : "A placa é única no sistema: se já existir, a tela avisa."
    };

    public string Contador => Passo == PassoVeiculo.Origem ? "Passo 1 de 2" : "Passo 2 de 2";

    public bool PodeVoltar => !Editando && Passo != PassoVeiculo.Origem;

    /// <summary>No passo da escolha não há formulário a salvar, e sim um vínculo a criar.</summary>
    public string TextoSalvar => Editando
        ? "Salvar alterações"
        : Passo == PassoVeiculo.Escolher ? "Vincular veículo" : "Cadastrar veículo";

    public bool MostrarSalvar => Passo != PassoVeiculo.Origem;
    public bool TemErro => Erro is not null;
    public bool TemAchado => Achado is not null;
    public bool SemResultado => !Procurando && Encontrados.Count == 0;

    [RelayCommand]
    private void Cancelar() => _aoCancelar?.Invoke();

    /// <summary>Passo 1: o carro já existe no sistema.</summary>
    [RelayCommand]
    private async Task EscolherExistente()
    {
        IrPara(PassoVeiculo.Escolher);
        await Procurar();
    }

    /// <summary>Passo 1: é um carro novo para o sistema.</summary>
    [RelayCommand]
    private void CadastrarNovo() => IrPara(PassoVeiculo.Dados);

    [RelayCommand]
    private void Voltar()
    {
        Escolhido = null;
        Achado = null;
        _encontrado = null;
        IrPara(PassoVeiculo.Origem);
    }

    [RelayCommand]
    private async Task Procurar()
    {
        Procurando = true;

        try
        {
            var achados = await _veiculos.ListarParaVincularAsync(_clienteId, Busca);

            Encontrados.Clear();
            foreach (var veiculo in achados)
            {
                Encontrados.Add(new VeiculoParaVincularViewModel(veiculo));
            }
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível buscar os veículos: {excecao.Message}";
        }
        finally
        {
            Procurando = false;
            OnPropertyChanged(nameof(SemResultado));
        }
    }

    [RelayCommand]
    private void Selecionar(VeiculoParaVincularViewModel? veiculo)
    {
        if (veiculo?.PodeLigar != true)
        {
            return;
        }

        foreach (var item in Encontrados)
        {
            item.Escolhido = ReferenceEquals(item, veiculo);
        }

        Escolhido = veiculo;
        Erro = null;
    }

    /// <summary>Liga a este cliente o veículo que já estava no sistema.</summary>
    [RelayCommand]
    private async Task UsarEncontrado()
    {
        if (_encontrado is null || Salvando)
        {
            return;
        }

        await Ligar(_encontrado.Id);
    }

    [RelayCommand]
    private async Task Salvar()
    {
        if (Salvando)
        {
            return;
        }

        // No passo da escolha não há nada a gravar no veículo: só o vínculo.
        if (Passo == PassoVeiculo.Escolher)
        {
            if (Escolhido is null)
            {
                Erro = "Escolha um veículo da lista.";
                return;
            }

            await Ligar(Escolhido.Id);
            return;
        }

        if (!await Conferir())
        {
            return;
        }

        Salvando = true;
        Erro = null;

        try
        {
            var veiculo = Montar();

            if (Editando)
            {
                await _veiculos.AtualizarAsync(veiculo);
            }
            else
            {
                veiculo.Id = await _veiculos.CadastrarComVinculoAsync(
                    veiculo, _clienteId, VinculoEscolhido());
            }

            _aoSalvar?.Invoke(veiculo.Id);
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível salvar: {excecao.Message}";
        }
        finally
        {
            Salvando = false;
        }
    }

    private async Task Ligar(int veiculoId)
    {
        Salvando = true;
        Erro = null;

        try
        {
            await _veiculos.VincularAsync(_clienteId, veiculoId, VinculoEscolhido());
            _aoSalvar?.Invoke(veiculoId);
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível ligar o veículo ao cliente: {excecao.Message}";
        }
        finally
        {
            Salvando = false;
        }
    }

    /// <summary>Confere a placa. Placa repetida não cadastra: o sistema já tem esse carro.</summary>
    private async Task<bool> Conferir()
    {
        ErroPlaca = null;
        Achado = null;
        Erro = null;
        _encontrado = null;

        var placa = Placa.Trim().ToUpperInvariant();

        if (placa.Length == 0)
        {
            ErroPlaca = "A placa é obrigatória.";
        }
        else if (placa.Length != 7)
        {
            ErroPlaca = "A placa tem 7 caracteres: ABC1234 ou ABC1D23.";
        }
        else if (!Editando)
        {
            var existente = await _veiculos.BuscarPorPlacaAsync(placa);
            if (existente is not null)
            {
                _encontrado = existente;
                Achado = $"A placa {placa} já está no sistema"
                       + (string.IsNullOrWhiteSpace(existente.MarcaModelo)
                            ? ". "
                            : $" ({existente.MarcaModelo}). ")
                       + "Não dá para cadastrar outra com a mesma placa — ligue este "
                       + "veículo ao cliente.";
            }
        }

        if (ErroPlaca is not null)
        {
            Erro = "Confira os campos marcados.";
            return false;
        }

        return Achado is null;
    }

    private Veiculo Montar() => new()
    {
        Id = _original?.Id ?? 0,
        Placa = Placa.Trim().ToUpperInvariant(),
        Renavam = Limpar(Normalizacao.SomenteDigitos(Renavam)),
        MarcaModelo = Limpar(MarcaModelo),
        Cor = Limpar(Cor),
        Ano = Limpar(Ano),
        Observacao = Limpar(Observacao)
    };

    private VinculoVeiculo VinculoEscolhido() =>
        Vinculo == "Condutor indicado" ? VinculoVeiculo.Condutor : VinculoVeiculo.Proprietario;

    private static string? Limpar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private void IrPara(PassoVeiculo passo)
    {
        Passo = passo;

        Passos[1].Titulo = passo == PassoVeiculo.Escolher ? "Escolher veículo" : "Dados do veículo";
        Passos[0].Atual = passo == PassoVeiculo.Origem;
        Passos[0].Concluido = passo != PassoVeiculo.Origem;
        Passos[1].Atual = passo != PassoVeiculo.Origem;
        Passos[1].Concluido = false;
    }

    partial void OnPassoChanged(PassoVeiculo valor)
    {
        OnPropertyChanged(nameof(NaOrigem));
        OnPropertyChanged(nameof(NaEscolha));
        OnPropertyChanged(nameof(NosDados));
        OnPropertyChanged(nameof(Instrucao));
        OnPropertyChanged(nameof(Contador));
        OnPropertyChanged(nameof(PodeVoltar));
        OnPropertyChanged(nameof(TextoSalvar));
        OnPropertyChanged(nameof(MostrarSalvar));
    }

    partial void OnErroChanged(string? valor) => OnPropertyChanged(nameof(TemErro));
    partial void OnAchadoChanged(string? valor) => OnPropertyChanged(nameof(TemAchado));
    partial void OnProcurandoChanged(bool valor) => OnPropertyChanged(nameof(SemResultado));

    // Mudar a placa invalida o aviso da placa anterior.
    partial void OnPlacaChanged(string valor)
    {
        Achado = null;
        _encontrado = null;
    }
}
