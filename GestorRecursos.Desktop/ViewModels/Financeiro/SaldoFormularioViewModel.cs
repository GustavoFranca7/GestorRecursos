using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Um cliente na lista de escolha do formulário de crédito.</summary>
public class EscolhaDeClienteViewModel
{
    public EscolhaDeClienteViewModel(ClienteParaEscolha dados)
    {
        Id = dados.Id;
        Nome = dados.Nome;
        Cpf = Formato.Cpf(dados.Cpf);

        Busca = $"{dados.Nome} {dados.Cpf} {Cpf}".ToLowerInvariant();
    }

    public int Id { get; }
    public string Nome { get; }
    public string Cpf { get; }
    public string Busca { get; }
}

/// <summary>Registra uma devolução como crédito do cliente (RF-40).</summary>
/// <remarks>
/// O crédito nasce de um dinheiro que voltou: serviço que não foi feito, cobrança a mais,
/// recurso que ela decidiu não levar adiante. Guardar isso é o ponto — o cliente some por
/// seis meses e volta, e sem registro ninguém lembra que havia valor a abater.
///
/// O crédito é do <b>cliente</b>, e não de uma contratação: ele costuma ser usado na
/// próxima, que ainda nem existe quando a devolução acontece.
/// </remarks>
public partial class SaldoFormularioViewModel : ViewModelBase
{
    private readonly PagamentoDAO _pagamentos = new();
    private readonly Action? _aoCancelar;
    private readonly Action? _aoSalvar;
    private readonly List<EscolhaDeClienteViewModel> _todos;

    public SaldoFormularioViewModel(
        List<ClienteParaEscolha> clientes,
        Action? aoCancelar = null,
        Action? aoSalvar = null)
    {
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;

        _todos = [.. clientes.Select(c => new EscolhaDeClienteViewModel(c))];
        Data = Formato.Data(DateTime.Today);

        Filtrar();
    }

    public ObservableCollection<EscolhaDeClienteViewModel> Clientes { get; } = [];

    [ObservableProperty] public partial string TermoBusca { get; set; } = string.Empty;
    [ObservableProperty] public partial EscolhaDeClienteViewModel? Escolhido { get; set; }
    [ObservableProperty] public partial string Valor { get; set; } = string.Empty;
    [ObservableProperty] public partial string Origem { get; set; } = string.Empty;
    [ObservableProperty] public partial string Data { get; set; }

    [ObservableProperty] public partial bool Salvando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? ErroValor { get; set; }
    [ObservableProperty] public partial string? ErroData { get; set; }
    [ObservableProperty] public partial string? ErroEscolha { get; set; }

    public bool TemErro => Erro is not null;
    public bool Escolhendo => Escolhido is null;
    public bool SemClientes => Clientes.Count == 0;

    partial void OnTermoBuscaChanged(string value) => Filtrar();

    partial void OnEscolhidoChanged(EscolhaDeClienteViewModel? value)
    {
        ErroEscolha = null;
        OnPropertyChanged(nameof(Escolhendo));
    }

    partial void OnErroChanged(string? value) => OnPropertyChanged(nameof(TemErro));

    [RelayCommand]
    private void Escolher(EscolhaDeClienteViewModel? cliente) => Escolhido = cliente;

    [RelayCommand]
    private void Trocar() => Escolhido = null;

    [RelayCommand]
    private void Cancelar() => _aoCancelar?.Invoke();

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
            await _pagamentos.RegistrarCreditoAsync(new Credito
            {
                ClienteId = Escolhido!.Id,
                Valor = Formato.LerDinheiro(Valor) ?? 0m,
                Origem = string.IsNullOrWhiteSpace(Origem) ? null : Origem.Trim(),
                Utilizado = false,
                DataCredito = Formato.LerData(Data) ?? DateTime.Today
            });

            _aoSalvar?.Invoke();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível registrar o saldo: {excecao.Message}";
        }
        finally
        {
            Salvando = false;
        }
    }

    private bool Conferir()
    {
        ErroEscolha = ErroValor = ErroData = Erro = null;

        if (Escolhido is null)
        {
            ErroEscolha = "Escolha de quem é o saldo.";
        }

        if (Formato.LerDinheiro(Valor) is not > 0)
        {
            ErroValor = "Informe um valor maior que zero.";
        }

        if (Formato.LerData(Data) is null)
        {
            ErroData = "Data inválida. Use dd/mm/aaaa.";
        }

        var tudoCerto = ErroEscolha is null && ErroValor is null && ErroData is null;

        if (!tudoCerto)
        {
            Erro = "Confira os campos marcados.";
        }

        return tudoCerto;
    }

    private void Filtrar()
    {
        var termo = TermoBusca.Trim().ToLowerInvariant();

        var filtrados = termo.Length == 0
            ? _todos
            : [.. _todos.Where(c => c.Busca.Contains(termo, StringComparison.Ordinal))];

        Clientes.Clear();
        foreach (var cliente in filtrados)
        {
            Clientes.Add(cliente);
        }

        OnPropertyChanged(nameof(SemClientes));
    }
}
