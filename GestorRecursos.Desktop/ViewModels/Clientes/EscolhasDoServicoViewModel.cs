using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Um veículo do cliente na escolha do passo de veículo.</summary>
public partial class VeiculoDoServicoViewModel(VeiculoDaFicha dados) : ObservableObject
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

/// <summary>Uma multa do veículo, na seleção do passo de multas.</summary>
public partial class MultaDoServicoViewModel : ObservableObject
{
    private readonly Action _aoMarcar;

    public MultaDoServicoViewModel(MultaParaRecurso dados, Action aoMarcar)
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

        // O prazo de defesa é o que decide a urgência; sem ele, não há alarme.
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

    /// <summary>Entra neste serviço. Duas vias: o clique na linha e o quadradinho.</summary>
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

    public bool JaContratada => Dados.JaTemRecurso;
    public bool PodeMarcar => !Dados.JaTemRecurso;
    public bool TemGravidade => Gravidade.Length > 0;

    partial void OnMarcadaChanged(bool valor) => _aoMarcar();
}
