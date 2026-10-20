using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Lança uma entrada de dinheiro numa contratação.</summary>
/// <remarks>
/// A janela nasce com o valor em aberto preenchido, porque o caso comum é quitar o que
/// falta. Pagamento parcial é só apagar e digitar outro valor.
/// </remarks>
public partial class PagamentoFormularioViewModel : ViewModelBase
{
    private readonly PagamentoDAO _pagamentos = new();
    private readonly Action? _aoCancelar;
    private readonly Action? _aoSalvar;
    private readonly int _contratacaoId;
    private readonly decimal _emAberto;

    public PagamentoFormularioViewModel(
        LancamentoDaFichaViewModel lancamento,
        Action? aoCancelar = null,
        Action? aoSalvar = null)
    {
        _contratacaoId = lancamento.Id;
        _emAberto = lancamento.Aberto;
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;

        Referencia = lancamento.Titulo;
        Contratado = lancamento.Contratado;
        JaPago = lancamento.Pago;
        EmAberto = lancamento.EmAberto;

        Data = Formato.Data(DateTime.Today);
        Valor = _emAberto > 0 ? _emAberto.ToString("N2", Brasil) : string.Empty;
    }

    private static readonly System.Globalization.CultureInfo Brasil = new("pt-BR");

    public string Referencia { get; }
    public string Contratado { get; }
    public string JaPago { get; }
    public string EmAberto { get; }

    public IReadOnlyList<string> Formas { get; } =
        ["Dinheiro", "PIX", "PIX parcelado", "Débito", "Crédito"];

    [ObservableProperty] public partial string Valor { get; set; }
    [ObservableProperty] public partial string Data { get; set; }
    [ObservableProperty] public partial string? Forma { get; set; } = "PIX";
    [ObservableProperty] public partial string Parcelas { get; set; } = "1";
    [ObservableProperty] public partial string Observacao { get; set; } = string.Empty;

    [ObservableProperty] public partial bool Salvando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? ErroValor { get; set; }
    [ObservableProperty] public partial string? ErroData { get; set; }

    public bool TemErro => Erro is not null;

    /// <summary>Parcela só existe onde o cliente paga em vezes.</summary>
    public bool Parcelavel => Forma is "Crédito" or "PIX parcelado";

    [RelayCommand]
    private void Cancelar() => _aoCancelar?.Invoke();

    /// <summary>Põe de volta o valor que falta, depois de uma digitação qualquer.</summary>
    [RelayCommand]
    private void Quitar() => Valor = _emAberto > 0 ? _emAberto.ToString("N2", Brasil) : "0,00";

    [RelayCommand]
    private async Task Salvar()
    {
        if (Salvando || !Conferir())
        {
            return;
        }

        Salvando = true;
        Erro = null;

        try
        {
            await _pagamentos.RegistrarAsync(new Pagamento
            {
                ContratacaoId = _contratacaoId,
                DataPagamento = Formato.LerData(Data) ?? DateTime.Today,
                Valor = Formato.LerDinheiro(Valor) ?? 0m,
                Forma = FormaEscolhida(),
                Parcelas = Parcelavel && int.TryParse(Parcelas, out var n) && n is >= 1 and <= 12 ? n : 1,
                Observacao = string.IsNullOrWhiteSpace(Observacao) ? null : Observacao.Trim()
            });

            _aoSalvar?.Invoke();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível lançar o pagamento: {excecao.Message}";
        }
        finally
        {
            Salvando = false;
        }
    }

    private bool Conferir()
    {
        ErroValor = ErroData = Erro = null;

        if (Formato.LerDinheiro(Valor) is not { } valor || valor <= 0)
        {
            ErroValor = "Informe um valor maior que zero.";
        }
        else if (valor > _emAberto)
        {
            // Receber mais do que o combinado vira crédito do cliente, não pagamento:
            // somado aqui, a contratação ficaria com saldo negativo e sem explicação.
            ErroValor = $"O valor excede o que está em aberto ({Formato.Dinheiro(_emAberto)}).";
        }

        if (Formato.LerData(Data) is not { } data)
        {
            ErroData = "Data inválida. Use dd/mm/aaaa.";
        }
        else if (data > DateTime.Today)
        {
            ErroData = "A data não pode ser futura.";
        }

        if (ErroValor is not null || ErroData is not null)
        {
            Erro = "Confira os campos marcados.";
            return false;
        }

        return true;
    }

    private FormaPagamento FormaEscolhida() => Forma switch
    {
        "Dinheiro" => FormaPagamento.Dinheiro,
        "PIX parcelado" => FormaPagamento.PixParcelado,
        "Débito" => FormaPagamento.Debito,
        "Crédito" => FormaPagamento.Credito,
        _ => FormaPagamento.Pix
    };

    partial void OnErroChanged(string? valor) => OnPropertyChanged(nameof(TemErro));
    partial void OnFormaChanged(string? valor) => OnPropertyChanged(nameof(Parcelavel));
}
