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
    private readonly bool _protocolada;

    public AndamentoFormularioViewModel(
        AcaoAndamento acao,
        ServicoDaFichaViewModel servico,
        FaseViewModel fase,
        Action? aoCancelar = null,
        Action? aoSalvar = null)
    {
        Acao = acao;
        _instanciaId = fase.Id;
        _fase = fase.Fase;
        _protocolada = fase.Protocolada;

        // Fase que nunca foi protocolada só pode ter caído: não houve peça para julgar.
        // Oferecer "deferido" ali abriria a porta para um estado que não existe no papel.
        if (!_protocolada) Resultado = "Anulado";
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;

        Referencia = $"{fase.Nome} · {servico.Titulo}";
        Data = Formato.Data(DateTime.Today);
        Protocolo = fase.Protocolo ?? string.Empty;
        Destinatario = fase.Destinatario ?? Sugerir(fase.Fase, servico.Dados.OrgaoAutuador);

        // O prazo da próxima fase conta da notificação do resultado. 30 dias é o mais
        // comum; fica como sugestão, porque quem manda é o papel que chegou.
        PrazoProximaFase = Formato.Data(DateTime.Today.AddDays(30));
    }

    public AcaoAndamento Acao { get; }

    /// <summary>Sem protocolo, o único desfecho possível é a fase ter sido anulada.</summary>
    public IReadOnlyList<string> Resultados =>
        _protocolada ? ["Deferido", "Indeferido", "Anulado"] : ["Anulado"];

    public bool Anulando => Resultado == "Anulado";

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

    public string Titulo => NoProtocolo
        ? "Registrar protocolo"
        : _protocolada ? "Registrar resultado" : "Anular esta fase";

    public string Instrucao => NoProtocolo
        ? "Protocolado é o que tira esta fase do alarme de prazo."
        : _protocolada
            ? "O que o órgão decidiu, e quando a decisão foi publicada."
            : "A fase caiu sem ser julgada. O processo segue na instância seguinte.";

    public string RotuloData => NoProtocolo
        ? "Data do protocolo *"
        : Anulando ? "Data da anulação" : "Data do resultado *";

    public string TextoSalvar => NoProtocolo
        ? "Registrar protocolo"
        : Anulando ? "Anular e seguir" : "Registrar resultado";

    /// <summary>Indeferido e anulado abrem a fase seguinte, que precisa de prazo para vigiar.</summary>
    public bool AbreProximaFase => NoResultado
        && Resultado is "Indeferido" or "Anulado"
        && _fase != FaseInstancia.Cetran;

    public string TextoProximaFase => (_fase, Anulando) switch
    {
        (FaseInstancia.Autuacao, true) => "Anulada a autuação, a JARI abre em seguida.",
        (FaseInstancia.Autuacao, false) => "Indeferida a autuação, a JARI abre em seguida.",
        (_, true) => "Anulada a JARI, o CETRAN abre em seguida.",
        _ => "Indeferida a JARI, o CETRAN abre em seguida."
    };

    public bool FimDaLinha => NoResultado
        && Resultado is "Indeferido" or "Anulado"
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
            var data = Formato.LerData(Data);

            if (NoProtocolo)
            {
                await _instancias.RegistrarProtocoloAsync(
                    _instanciaId, data!.Value, Protocolo.Trim(), Limpar(Destinatario));
            }
            else
            {
                await _instancias.RegistrarResultadoAsync(
                    _instanciaId,
                    Resultado switch
                    {
                        "Deferido" => ResultadoInstancia.Deferido,
                        "Anulado" => ResultadoInstancia.Anulado,
                        _ => ResultadoInstancia.Indeferido
                    },
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

        // Anular não exige data: a fase pode cair antes de existir qualquer peça, e então
        // não há decisão publicada de onde tirar uma. Digitada errada, ainda é erro.
        var dataOpcional = NoResultado && Anulando;

        if (Formato.LerData(Data) is null && !(dataOpcional && Data.Trim().Length == 0))
        {
            ErroData = "Data inválida. Use dd/mm/aaaa.";
        }

        if (NoProtocolo && Protocolo.Trim().Length == 0)
        {
            ErroProtocolo = "O número do protocolo é obrigatório.";
        }

        if (NoResultado && Resultado is null)
        {
            Erro = "Selecione o resultado do julgamento.";
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
        OnPropertyChanged(nameof(Anulando));
        OnPropertyChanged(nameof(AbreProximaFase));
        OnPropertyChanged(nameof(FimDaLinha));
        OnPropertyChanged(nameof(RotuloData));
        OnPropertyChanged(nameof(TextoSalvar));
        OnPropertyChanged(nameof(TextoProximaFase));
    }
}
