using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Corrige as duas datas de uma etapa do roteiro.</summary>
/// <remarks>
/// Marcar a caixa grava a conclusão como hoje, que é o caso comum. Esta janela existe
/// para os outros: pôr o prazo que a etapa tem, e consertar a data de quando algo foi
/// feito na semana passada e só agora está sendo lançado.
///
/// O prazo é o que vai alimentar a agenda, então ele é o campo que mais importa aqui.
/// </remarks>
public partial class EtapaFormularioViewModel : ViewModelBase
{
    private readonly EtapaDAO _etapas = new();
    private readonly Action? _aoCancelar;
    private readonly Action? _aoSalvar;
    private readonly int _etapaId;

    public EtapaFormularioViewModel(
        EtapaViewModel etapa,
        Action? aoCancelar = null,
        Action? aoSalvar = null)
    {
        _etapaId = etapa.Id;
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;

        Nome = etapa.Nome;
        Referencia = etapa.Servico?.Titulo ?? string.Empty;
        Prazo = etapa.Dados.Prazo is { } prazo ? Formato.Data(prazo) : string.Empty;
        Conclusao = etapa.Dados.DataConclusao is { } feito ? Formato.Data(feito) : string.Empty;
    }

    public string Nome { get; }
    public string Referencia { get; }

    [ObservableProperty] public partial string Prazo { get; set; }
    [ObservableProperty] public partial string Conclusao { get; set; }
    [ObservableProperty] public partial bool Salvando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? ErroPrazo { get; set; }
    [ObservableProperty] public partial string? ErroConclusao { get; set; }

    public bool TemErro => Erro is not null;

    [RelayCommand]
    private void Cancelar() => _aoCancelar?.Invoke();

    /// <summary>Marca a etapa como feita hoje, sem precisar digitar a data.</summary>
    [RelayCommand]
    private void Hoje() => Conclusao = Formato.Data(DateTime.Today);

    /// <summary>Devolve a etapa ao estado de pendente.</summary>
    [RelayCommand]
    private void Reabrir() => Conclusao = string.Empty;

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
            await _etapas.AtualizarDatasAsync(
                _etapaId, Formato.LerData(Prazo), Formato.LerData(Conclusao));

            _aoSalvar?.Invoke();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível salvar as datas: {excecao.Message}";
        }
        finally
        {
            Salvando = false;
        }
    }

    /// <summary>Campo em branco é "não sei ainda", e isso é um estado válido.</summary>
    private bool Conferir()
    {
        ErroPrazo = ErroConclusao = Erro = null;

        if (Prazo.Trim().Length > 0 && Formato.LerData(Prazo) is null)
        {
            ErroPrazo = "Data inválida. Use dd/mm/aaaa.";
        }

        if (Conclusao.Trim().Length > 0 && Formato.LerData(Conclusao) is null)
        {
            ErroConclusao = "Data inválida. Use dd/mm/aaaa.";
        }

        if (ErroPrazo is not null || ErroConclusao is not null)
        {
            Erro = "Confira os campos marcados.";
            return false;
        }

        return true;
    }

    partial void OnErroChanged(string? valor) => OnPropertyChanged(nameof(TemErro));
}
