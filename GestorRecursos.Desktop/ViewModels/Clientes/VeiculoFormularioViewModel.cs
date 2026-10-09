using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;
using GestorRecursos.Core.Validacao;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Cadastro e edição de veículo, ligado a um cliente.</summary>
/// <remarks>
/// O veículo existe uma vez só no sistema, pela placa, e é ligado a quantos clientes
/// precisar: o mesmo carro pode ter o dono num recurso e o condutor indicado em outro.
/// Por isso, digitar uma placa que já existe não é erro — é o pedido de ligá-la a este
/// cliente, e é o que esta tela oferece.
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
    }

    public IReadOnlyList<string> Vinculos { get; } = ["Proprietário", "Condutor indicado"];

    [ObservableProperty] public partial string Placa { get; set; } = string.Empty;
    [ObservableProperty] public partial string Renavam { get; set; } = string.Empty;
    [ObservableProperty] public partial string MarcaModelo { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cor { get; set; } = string.Empty;
    [ObservableProperty] public partial string Ano { get; set; } = string.Empty;
    [ObservableProperty] public partial string Observacao { get; set; } = string.Empty;

    [ObservableProperty] public partial string? Vinculo { get; set; } = "Proprietário";

    [ObservableProperty] public partial bool Salvando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? ErroPlaca { get; set; }

    /// <summary>Aviso de placa já cadastrada, com o convite para ligá-la a este cliente.</summary>
    [ObservableProperty] public partial string? Achado { get; set; }

    public bool Editando => _original is not null;
    public string Titulo => Editando ? "Editar veículo" : "Novo veículo";
    public string TextoSalvar => Editando ? "Salvar alterações" : "Cadastrar veículo";
    public bool TemErro => Erro is not null;
    public bool TemAchado => Achado is not null;

    [RelayCommand]
    private void Cancelar() => _aoCancelar?.Invoke();

    /// <summary>Liga a este cliente o veículo que já estava no sistema.</summary>
    [RelayCommand]
    private async Task UsarEncontrado()
    {
        if (_encontrado is null || Salvando)
        {
            return;
        }

        Salvando = true;

        try
        {
            await _veiculos.VincularAsync(_clienteId, _encontrado.Id, VinculoEscolhido());
            _aoSalvar?.Invoke(_encontrado.Id);
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

    [RelayCommand]
    private async Task Salvar()
    {
        if (Salvando || !await Conferir())
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
            // Placa repetida não é erro: é um carro que o sistema já conhece.
            var existente = await _veiculos.BuscarPorPlacaAsync(placa);
            if (existente is not null)
            {
                _encontrado = existente;
                Achado = $"A placa {placa} já está no sistema"
                       + (string.IsNullOrWhiteSpace(existente.MarcaModelo)
                            ? ". "
                            : $" ({existente.MarcaModelo}). ")
                       + "Ligar este veículo ao cliente em vez de cadastrar outro?";
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

    partial void OnErroChanged(string? valor) => OnPropertyChanged(nameof(TemErro));
    partial void OnAchadoChanged(string? valor) => OnPropertyChanged(nameof(TemAchado));

    // Mudar a placa invalida o aviso da placa anterior.
    partial void OnPlacaChanged(string valor)
    {
        Achado = null;
        _encontrado = null;
    }
}
