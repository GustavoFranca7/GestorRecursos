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

/// <summary>Uma contratação na lista de escolha do formulário de despesa.</summary>
public class EscolhaDeContratacaoViewModel
{
    public EscolhaDeContratacaoViewModel(ContratacaoParaLancamento dados)
    {
        Id = dados.ContratacaoId;
        Nome = dados.ClienteNome;
        Detalhe = MontarDetalhe(dados);

        Busca = $"{dados.ClienteNome} {dados.ClienteCpf} {Formato.Cpf(dados.ClienteCpf)} {dados.Servicos}"
            .ToLowerInvariant();
    }

    public int Id { get; }
    public string Nome { get; }

    /// <summary>"Recurso de multa grave · 21/09/2026 · R$ 40,00", para achar a certa.</summary>
    public string Detalhe { get; }

    public string Busca { get; }

    private static string MontarDetalhe(ContratacaoParaLancamento dados)
    {
        var partes = new List<string>();

        if (!string.IsNullOrWhiteSpace(dados.Servicos)) partes.Add(dados.Servicos!);
        partes.Add(Formato.Data(dados.DataContratacao));
        partes.Add(Formato.Dinheiro(dados.ValorFinal));

        // Quem já tem custo lançado mostra quanto: é o aviso contra lançar em duplicata.
        if (dados.TotalDespesas > 0)
        {
            partes.Add($"já tem {Formato.Dinheiro(dados.TotalDespesas)} de despesa");
        }

        return string.Join(" · ", partes);
    }
}

/// <summary>Lança um custo do serviço: correio, taxa de DETRAN, autenticação (RF-39).</summary>
/// <remarks>
/// A despesa é de uma contratação, e não de um cliente, porque é por contratação que o
/// resultado líquido é calculado. Por isso o formulário começa pela escolha: quem tem três
/// serviços contratados precisa dizer em qual o correio foi gasto.
///
/// O tipo é texto livre, como a coluna no banco. Os botões de cima são só atalhos para o
/// que se repete toda semana — não são uma lista fechada, porque a próxima taxa que o
/// DETRAN inventar não vai estar nela.
/// </remarks>
public partial class DespesaFormularioViewModel : ViewModelBase
{
    private readonly PagamentoDAO _pagamentos = new();
    private readonly Action? _aoCancelar;
    private readonly Action? _aoSalvar;
    private readonly List<EscolhaDeContratacaoViewModel> _todas;

    public DespesaFormularioViewModel(
        List<ContratacaoParaLancamento> contratacoes,
        Action? aoCancelar = null,
        Action? aoSalvar = null)
    {
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;

        _todas = [.. contratacoes.Select(c => new EscolhaDeContratacaoViewModel(c))];
        Data = Formato.Data(DateTime.Today);

        Filtrar();
    }

    public ObservableCollection<EscolhaDeContratacaoViewModel> Contratacoes { get; } = [];

    public IReadOnlyList<string> TiposComuns { get; } =
        ["Correio", "Taxa de DETRAN", "Autenticação", "Cópias"];

    [ObservableProperty] public partial string TermoBusca { get; set; } = string.Empty;
    [ObservableProperty] public partial EscolhaDeContratacaoViewModel? Escolhida { get; set; }
    [ObservableProperty] public partial string Tipo { get; set; } = string.Empty;
    [ObservableProperty] public partial string Valor { get; set; } = string.Empty;
    [ObservableProperty] public partial string Data { get; set; }

    [ObservableProperty] public partial bool Salvando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? ErroTipo { get; set; }
    [ObservableProperty] public partial string? ErroValor { get; set; }
    [ObservableProperty] public partial string? ErroData { get; set; }
    [ObservableProperty] public partial string? ErroEscolha { get; set; }

    public bool TemErro => Erro is not null;

    /// <summary>Escolhida a contratação, a lista some e o resto do formulário aparece.</summary>
    public bool Escolhendo => Escolhida is null;

    public bool SemContratacoes => Contratacoes.Count == 0;

    partial void OnTermoBuscaChanged(string value) => Filtrar();

    partial void OnEscolhidaChanged(EscolhaDeContratacaoViewModel? value)
    {
        ErroEscolha = null;
        OnPropertyChanged(nameof(Escolhendo));
    }

    partial void OnErroChanged(string? value) => OnPropertyChanged(nameof(TemErro));

    [RelayCommand]
    private void Escolher(EscolhaDeContratacaoViewModel? contratacao) => Escolhida = contratacao;

    /// <summary>Volta à lista para corrigir a contratação escolhida.</summary>
    [RelayCommand]
    private void Trocar() => Escolhida = null;

    [RelayCommand]
    private void UsarTipo(string? tipo) => Tipo = tipo ?? string.Empty;

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
            await _pagamentos.RegistrarDespesaAsync(new Despesa
            {
                ContratacaoId = Escolhida!.Id,
                Tipo = Tipo.Trim(),
                Valor = Formato.LerDinheiro(Valor) ?? 0m,
                DataDespesa = Formato.LerData(Data) ?? DateTime.Today
            });

            _aoSalvar?.Invoke();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível lançar a despesa: {excecao.Message}";
        }
        finally
        {
            Salvando = false;
        }
    }

    private bool Conferir()
    {
        ErroEscolha = ErroTipo = ErroValor = ErroData = Erro = null;

        if (Escolhida is null)
        {
            ErroEscolha = "Escolha a contratação em que a despesa foi gasta.";
        }

        if (string.IsNullOrWhiteSpace(Tipo))
        {
            ErroTipo = "Diga o que foi essa despesa.";
        }

        // Zero é recusado de propósito: despesa de zero não é despesa, é engano.
        if (Formato.LerDinheiro(Valor) is not > 0)
        {
            ErroValor = "Informe um valor maior que zero.";
        }

        if (Formato.LerData(Data) is null)
        {
            ErroData = "Data inválida. Use dd/mm/aaaa.";
        }

        var tudoCerto = ErroEscolha is null && ErroTipo is null
                        && ErroValor is null && ErroData is null;

        if (!tudoCerto)
        {
            Erro = "Confira os campos marcados.";
        }

        return tudoCerto;
    }

    private void Filtrar()
    {
        var termo = TermoBusca.Trim().ToLowerInvariant();

        var filtradas = termo.Length == 0
            ? _todas
            : [.. _todas.Where(c => c.Busca.Contains(termo, StringComparison.Ordinal))];

        Contratacoes.Clear();
        foreach (var contratacao in filtradas)
        {
            Contratacoes.Add(contratacao);
        }

        OnPropertyChanged(nameof(SemContratacoes));
    }
}
