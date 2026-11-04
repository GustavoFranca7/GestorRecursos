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

/// <summary>Uma linha da tabela de preços, com a edição do valor embutida.</summary>
/// <remarks>
/// Editar na própria linha, e não numa janela por cima: reajuste costuma ser de vários
/// serviços seguidos, e abrir e fechar janela a cada um transformaria dez minutos de
/// trabalho em meia hora.
/// </remarks>
public partial class LinhaPrecoViewModel : ObservableObject
{
    private readonly TipoServico _dados;
    private readonly Func<LinhaPrecoViewModel, decimal, Task> _aoSalvar;

    public LinhaPrecoViewModel(TipoServico dados, Func<LinhaPrecoViewModel, decimal, Task> aoSalvar)
    {
        _dados = dados;
        _aoSalvar = aoSalvar;
        Valor = dados.ValorTabela;
    }

    public int Id => _dados.Id;
    public string Nome => _dados.Nome;
    public bool Ativo => _dados.Ativo;
    public string Grupo => _dados.Fluxo.Grupo();

    /// <summary>"3 fases · 5 etapas", o que o serviço percorre depois de contratado.</summary>
    public string Percurso
    {
        get
        {
            var partes = new List<string>();

            if (_dados.TemInstancias) partes.Add("autuação, JARI e CETRAN");
            if (_dados.QuantidadeEtapas > 0) partes.Add($"{_dados.QuantidadeEtapas} etapas");
            if (!_dados.Ativo) partes.Add("fora de uso");

            return string.Join(" · ", partes);
        }
    }

    public bool TemPercurso => Percurso.Length > 0;

    [ObservableProperty] public partial decimal Valor { get; set; }
    [ObservableProperty] public partial bool Editando { get; set; }
    [ObservableProperty] public partial bool Salvando { get; set; }
    [ObservableProperty] public partial string ValorDigitado { get; set; } = string.Empty;
    [ObservableProperty] public partial string? Erro { get; set; }

    public string ValorLegivel => Formato.Dinheiro(Valor);
    public bool TemErro => !string.IsNullOrWhiteSpace(Erro);

    [RelayCommand]
    private void Editar()
    {
        Erro = null;

        // Sem o "R$" e sem separador de milhar: o campo é para digitar, não para ler.
        ValorDigitado = Formato.DinheiroSimples(Valor);
        Editando = true;
    }

    [RelayCommand]
    private void Cancelar()
    {
        Editando = false;
        Erro = null;
    }

    [RelayCommand]
    private async Task Salvar()
    {
        if (Formato.LerDinheiro(ValorDigitado) is not { } novo || novo < 0)
        {
            Erro = "Informe um valor, como 350 ou 350,00.";
            OnPropertyChanged(nameof(TemErro));
            return;
        }

        Salvando = true;
        Erro = null;
        OnPropertyChanged(nameof(TemErro));

        try
        {
            await _aoSalvar(this, novo);

            Valor = novo;
            _dados.ValorTabela = novo;
            Editando = false;
            OnPropertyChanged(nameof(ValorLegivel));
        }
        catch (Exception excecao)
        {
            Erro = excecao.Message;
            OnPropertyChanged(nameof(TemErro));
        }
        finally
        {
            Salvando = false;
        }
    }
}

/// <summary>Um bloco da tabela de preços: MULTAS, CNH, VEÍCULO, OUTROS.</summary>
public class GrupoDePrecosViewModel(string titulo, IEnumerable<LinhaPrecoViewModel> linhas)
{
    public string Titulo { get; } = titulo;
    public IReadOnlyList<LinhaPrecoViewModel> Linhas { get; } = linhas.ToList();
}

/// <summary>A tabela de preços do escritório, editável na tela (RF-52).</summary>
/// <remarks>
/// Existe para tirar a dependência de quem programa: antes, reajustar um valor era rodar
/// um UPDATE à mão. O preço que muda aqui é o de tabela, isto é, a referência do dia —
/// contratação já fechada guarda o valor da época e não se mexe.
/// </remarks>
public partial class ServicosViewModel : ViewModelBase
{
    private readonly TipoServicoDAO _tipos = new();

    public ServicosViewModel()
    {
        // No preview do Rider não há banco nem wallet: a tela abriria com erro de conexão.
        if (!Design.IsDesignMode)
        {
            CarregarCommand.Execute(null);
        }
    }

    public ObservableCollection<GrupoDePrecosViewModel> Grupos { get; } = [];

    [ObservableProperty] public partial bool Carregando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    public bool SemServicos => !Carregando && Erro is null && Grupos.Count == 0;

    [RelayCommand]
    private async Task Carregar()
    {
        Carregando = true;
        Erro = null;

        try
        {
            // Inclui os desativados: um serviço fora de uso ainda tem preço, e escondê-lo
            // faria a tela parecer incompleta na hora de conferir a tabela inteira.
            var lista = await _tipos.ListarAsync(apenasAtivos: false);

            Grupos.Clear();

            foreach (var grupo in lista
                         .Select(t => new LinhaPrecoViewModel(t, Gravar))
                         .GroupBy(l => l.Grupo)
                         .OrderBy(g => OrdemDoGrupo(g.Key)))
            {
                Grupos.Add(new GrupoDePrecosViewModel(grupo.Key, grupo));
            }
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível carregar a tabela de preços: {excecao.Message}";
        }
        finally
        {
            Carregando = false;
            OnPropertyChanged(nameof(SemServicos));
        }
    }

    private async Task Gravar(LinhaPrecoViewModel linha, decimal novo)
    {
        await _tipos.AtualizarValorAsync(linha.Id, novo);
        Notificacoes.Sucesso($"{linha.Nome}: {Formato.Dinheiro(novo)}");
    }

    // Multas primeiro porque e o que ela mais vende; "outros" por ultimo.
    private static int OrdemDoGrupo(string grupo) => grupo switch
    {
        "MULTAS" => 0,
        "CNH" => 1,
        "VEÍCULO" => 2,
        _ => 3
    };

    partial void OnCarregandoChanged(bool valor) => OnPropertyChanged(nameof(SemServicos));
}
