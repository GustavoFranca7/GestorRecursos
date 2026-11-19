using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Cadastra ou altera um veículo, sem passar por cliente nenhum.</summary>
/// <remarks>
/// É irmão do formulário da ficha (<c>VeiculoFormularioViewModel</c>), e não o mesmo: lá
/// o carro nasce ligado a um cliente, com escolha de vínculo e de veículo já existente,
/// porque aquele caminho começa no cadastro de uma multa. Aqui o carro pode nascer solto
/// — é o caso da placa que chega numa notificação antes de o dono procurar o escritório.
///
/// Juntar os dois num formulário só com metade dos campos escondidos sairia mais confuso
/// que dois formulários curtos.
/// </remarks>
public partial class CadastroDeVeiculoViewModel : ViewModelBase
{
    private readonly VeiculoDAO _veiculos = new();
    private readonly Action? _aoCancelar;
    private readonly Action? _aoSalvar;

    private readonly int _id;

    /// <summary>Veículo novo.</summary>
    public CadastroDeVeiculoViewModel(Action? aoCancelar = null, Action? aoSalvar = null)
    {
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;
    }

    /// <summary>Alteração de um veículo que já existe.</summary>
    public CadastroDeVeiculoViewModel(
        VeiculoDaLista dados,
        Action? aoCancelar = null,
        Action? aoSalvar = null)
    {
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;
        _id = dados.Id;

        Placa = dados.Placa;
        Renavam = dados.Renavam ?? string.Empty;
        MarcaModelo = dados.MarcaModelo ?? string.Empty;
        Cor = dados.Cor ?? string.Empty;
        Ano = dados.Ano ?? string.Empty;
        Observacao = dados.Observacao ?? string.Empty;
    }

    [ObservableProperty] public partial string Placa { get; set; } = string.Empty;
    [ObservableProperty] public partial string Renavam { get; set; } = string.Empty;
    [ObservableProperty] public partial string MarcaModelo { get; set; } = string.Empty;
    [ObservableProperty] public partial string Cor { get; set; } = string.Empty;
    [ObservableProperty] public partial string Ano { get; set; } = string.Empty;
    [ObservableProperty] public partial string Observacao { get; set; } = string.Empty;

    [ObservableProperty] public partial bool Salvando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? ErroPlaca { get; set; }

    public bool Editando => _id > 0;
    public bool TemErro => Erro is not null;

    public string TituloDaJanela => Editando ? "Alterar veículo" : "Novo veículo";
    public string TextoDoBotao => Editando ? "Salvar" : "Cadastrar";

    public string Explicacao => Editando
        ? "Marca/modelo, cor, ano e RENAVAM são o que o texto do recurso usa para descrever o carro."
        : "Só a placa é obrigatória. O resto o recurso usa para descrever o carro, e pode entrar depois.";

    partial void OnErroChanged(string? value) => OnPropertyChanged(nameof(TemErro));

    [RelayCommand]
    private void Cancelar() => _aoCancelar?.Invoke();

    [RelayCommand]
    private async Task Salvar()
    {
        var placa = Placa.Trim().ToUpperInvariant().Replace("-", string.Empty);

        ErroPlaca = placa.Length is < 7 or > 7 ? "A placa tem 7 caracteres." : null;

        if (ErroPlaca is not null)
        {
            return;
        }

        Salvando = true;
        Erro = null;

        try
        {
            // Avisa antes do UNIQUE: "placa já cadastrada" é mais útil que o erro do banco.
            if (await _veiculos.PlacaJaCadastradaAsync(placa, _id))
            {
                ErroPlaca = "Esta placa já está cadastrada.";
                return;
            }

            var veiculo = new Veiculo
            {
                Id = _id,
                Placa = placa,
                Renavam = Vazio(Renavam),
                MarcaModelo = Vazio(MarcaModelo),
                Cor = Vazio(Cor),
                Ano = Vazio(Ano),
                Observacao = Vazio(Observacao)
            };

            if (Editando)
            {
                await _veiculos.AtualizarAsync(veiculo);
                Notificacoes.Sucesso("Veículo alterado.");
            }
            else
            {
                await _veiculos.CadastrarAsync(veiculo);
                Notificacoes.Sucesso($"Veículo {placa} cadastrado.");
            }

            _aoSalvar?.Invoke();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível salvar o veículo: {excecao.Message}";
        }
        finally
        {
            Salvando = false;
        }
    }

    /// <summary>Campo em branco vira nulo: no banco, "não informado" é ausência.</summary>
    private static string? Vazio(string texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
