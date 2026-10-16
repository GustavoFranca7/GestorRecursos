using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>O que está sendo registrado numa fase do recurso.</summary>
public enum AcaoAndamento
{
    Protocolo,
    Resultado
}

/// <summary>Faz o recurso andar: registra o protocolo de uma fase ou o julgamento dela.</summary>
/// <remarks>
/// Os dois caminhos moram na mesma janela porque são o mesmo gesto do dia a dia — "chegou
/// notícia desta fase" — e porque a moldura, o título e o rodapé seriam copiados iguais.
///
/// Registrar indeferido abre a fase seguinte sozinho: o valor contratado já cobre as três
/// instâncias, então subir de fase não é decisão comercial, é a continuação do serviço.
/// </remarks>
public partial class AndamentoFormularioViewModel : ViewModelBase
{
    private readonly InstanciaDAO _instancias = new();
    private readonly Action? _aoCancelar;
    private readonly Action? _aoSalvar;
    private readonly int _instanciaId;
    private readonly FaseInstancia _fase;

    public AndamentoFormularioViewModel(
        AcaoAndamento acao,
        RecursoDaFichaViewModel recurso,
        FaseViewModel fase,
        Action? aoCancelar = null,
        Action? aoSalvar = null)
    {
        Acao = acao;
        _instanciaId = fase.Id;
        _fase = fase.Fase;
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;

        Referencia = $"{fase.Nome} · {recurso.Titulo}";
        Data = Formato.Data(DateTime.Today);
        Protocolo = fase.Protocolo ?? string.Empty;
        Destinatario = fase.Destinatario ?? Sugerir(fase.Fase, recurso.Dados.OrgaoAutuador);

        // O prazo da próxima fase conta da notificação do resultado. 30 dias é o mais
        // comum; fica como sugestão, porque quem manda é o papel que chegou.
        PrazoProximaFase = Formato.Data(DateTime.Today.AddDays(30));
    }

    public AcaoAndamento Acao { get; }

    public IReadOnlyList<string> Resultados { get; } = ["Deferido", "Indeferido"];

    /// <summary>"JARI · RECURSO DE MULTA GRAVÍSSIMA · AIT ...": de qual fase se fala.</summary>
    public string Referencia { get; }

    [ObservableProperty] public partial string Data { get; set; }
    [ObservableProperty] public partial string Protocolo { get; set; }
    [ObservableProperty] public partial string Destinatario { get; set; }
    [ObservableProperty] public partial string? Resultado { get; set; }
    [ObservableProperty] public partial string PrazoProximaFase { get; set; }
    [ObservableProperty] public partial string Observacoes { get; set; } = string.Empty;

    [ObservableProperty] public partial bool Salvando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? ErroData { get; set; }
    [ObservableProperty] public partial string? ErroProtocolo { get; set; }

    public bool NoProtocolo => Acao == AcaoAndamento.Protocolo;
    public bool NoResultado => Acao == AcaoAndamento.Resultado;

    public string Titulo => NoProtocolo ? "Registrar protocolo" : "Registrar resultado";

    public string Instrucao => NoProtocolo
        ? "Protocolado é o que tira esta fase do alarme de prazo."
        : "O que o órgão decidiu, e quando a decisão foi publicada.";

    public string RotuloData => NoProtocolo ? "Data do protocolo *" : "Data do resultado *";
    public string TextoSalvar => NoProtocolo ? "Registrar protocolo" : "Registrar resultado";

    /// <summary>Indeferido abre a fase seguinte — e ela precisa de um prazo para vigiar.</summary>
    public bool AbreProximaFase => NoResultado
        && Resultado == "Indeferido"
        && _fase != FaseInstancia.Cetran;

    public string TextoProximaFase => _fase == FaseInstancia.Autuacao
        ? "Indeferida a autuação, a JARI abre em seguida."
        : "Indeferida a JARI, o CETRAN abre em seguida.";

    public bool FimDaLinha => NoResultado
        && Resultado == "Indeferido"
        && _fase == FaseInstancia.Cetran;

    public bool TemErro => Erro is not null;

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
            var data = Formato.LerData(Data)!.Value;

            if (NoProtocolo)
            {
                await _instancias.RegistrarProtocoloAsync(
                    _instanciaId, data, Protocolo.Trim(), Limpar(Destinatario));
            }
            else
            {
                await _instancias.RegistrarResultadoAsync(
                    _instanciaId,
                    Resultado == "Deferido" ? ResultadoInstancia.Deferido : ResultadoInstancia.Indeferido,
                    data,
                    AbreProximaFase ? Formato.LerData(PrazoProximaFase) : null);
            }

            _aoSalvar?.Invoke();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível registrar: {excecao.Message}";
        }
        finally
        {
            Salvando = false;
        }
    }

    private bool Conferir()
    {
        ErroData = ErroProtocolo = Erro = null;

        if (Formato.LerData(Data) is null)
        {
            ErroData = "Data inválida. Use dd/mm/aaaa.";
        }

        if (NoProtocolo && Protocolo.Trim().Length == 0)
        {
            ErroProtocolo = "O número do protocolo é obrigatório.";
        }

        if (NoResultado && Resultado is null)
        {
            Erro = "Escolha se foi deferido ou indeferido.";
            return false;
        }

        if (ErroData is not null || ErroProtocolo is not null)
        {
            Erro = "Confira os campos marcados.";
            return false;
        }

        return true;
    }

    /// <summary>Para quem o recurso vai, por fase. Poupa digitação no caso de sempre.</summary>
    private static string Sugerir(FaseInstancia fase, string? orgaoAutuador) => fase switch
    {
        FaseInstancia.Jari => "Presidente da JARI",
        FaseInstancia.Cetran => "Presidente do CETRAN",
        _ => orgaoAutuador ?? string.Empty
    };

    private static string? Limpar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    partial void OnErroChanged(string? valor) => OnPropertyChanged(nameof(TemErro));

    partial void OnResultadoChanged(string? valor)
    {
        OnPropertyChanged(nameof(AbreProximaFase));
        OnPropertyChanged(nameof(FimDaLinha));
    }
}
