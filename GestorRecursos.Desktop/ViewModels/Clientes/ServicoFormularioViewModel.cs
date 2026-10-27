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

/// <summary>Uma das três fases do recurso, aberta para preenchimento de uma vez só.</summary>
/// <remarks>
/// A situação não é campo: ela sai do que foi preenchido. Com resultado, está julgada;
/// com protocolo e sem resultado, aguardando julgamento; sem nada, esperando protocolo.
/// </remarks>
public partial class FaseEditavelViewModel : ObservableObject
{
    private readonly Action _aoMudar;

    public FaseEditavelViewModel(FaseInstancia fase, Instancia? dados, Action aoMudar)
    {
        _aoMudar = aoMudar;
        Fase = fase;
        Id = dados?.Id ?? 0;
        Nome = fase switch
        {
            FaseInstancia.Autuacao => "1 · AUTUAÇÃO",
            FaseInstancia.Jari => "2 · JARI",
            _ => "3 · CETRAN"
        };

        Prazo = Formato.Data(dados?.PrazoProtocolo);
        Protocolo = dados?.NumeroProtocolo ?? string.Empty;
        Enviado = Formato.Data(dados?.DataProtocolo);
        Destinatario = dados?.OrgaoDestinatario ?? Sugerir(fase);
        Julgado = Formato.Data(dados?.DataResultado);
        Resultado = dados?.Resultado switch
        {
            ResultadoInstancia.Deferido => "Deferido",
            ResultadoInstancia.Indeferido => "Indeferido",
            ResultadoInstancia.Anulado => "Anulado",
            _ => null
        };
    }

    public FaseInstancia Fase { get; }
    public int Id { get; }
    public string Nome { get; }

    public IReadOnlyList<string> Resultados { get; } = ["Deferido", "Indeferido", "Anulado"];

    [ObservableProperty] public partial string Prazo { get; set; }
    [ObservableProperty] public partial string Protocolo { get; set; }
    [ObservableProperty] public partial string Enviado { get; set; }
    [ObservableProperty] public partial string Destinatario { get; set; }
    [ObservableProperty] public partial string Julgado { get; set; }
    [ObservableProperty] public partial string? Resultado { get; set; }

    /// <summary>Fase que uma decisão anterior já encerrou: não há o que preencher nela.</summary>
    [ObservableProperty] public partial bool Bloqueada { get; set; }

    public bool Liberada => !Bloqueada;

    /// <summary>Por que a fase está fechada. Vazio quando ela está aberta.</summary>
    [ObservableProperty] public partial string Motivo { get; set; } = string.Empty;

    public bool TemMotivo => Motivo.Length > 0;

    public bool Indeferida => Resultado == "Indeferido";
    public bool Deferida => Resultado == "Deferido";

    /// <summary>Anulada abre a fase seguinte, como a indeferida.</summary>
    public bool Anulada => Resultado == "Anulado";

    /// <summary>O que vai para o banco, com a situação deduzida do preenchimento.</summary>
    public Instancia Montar() => new()
    {
        Id = Id,
        Fase = Fase,
        PrazoProtocolo = Formato.LerData(Prazo),
        DataProtocolo = Formato.LerData(Enviado),
        NumeroProtocolo = Limpar(Protocolo),
        OrgaoDestinatario = Limpar(Destinatario),
        DataResultado = Formato.LerData(Julgado),
        Resultado = Resultado switch
        {
            "Deferido" => ResultadoInstancia.Deferido,
            "Indeferido" => ResultadoInstancia.Indeferido,
            "Anulado" => ResultadoInstancia.Anulado,
            _ => null
        }
    };

    /// <summary>As datas digitadas fazem sentido? Devolve o primeiro problema.</summary>
    public string? Conferir()
    {
        foreach (var (texto, campo) in new[]
                 {
                     (Prazo, "prazo"), (Enviado, "data de envio"), (Julgado, "data do resultado")
                 })
        {
            if (texto.Trim().Length > 0 && Formato.LerData(texto) is null)
            {
                return $"{Nome}: {campo} inválida. Use dd/mm/aaaa.";
            }
        }

        // Anulada não tem data a exigir: a fase pode cair antes de qualquer peça existir,
        // e aí não houve decisão publicada de onde tirar uma.
        if (Resultado is not null && !Anulada && Formato.LerData(Julgado) is null)
        {
            return $"{Nome}: informe a data do resultado.";
        }

        return null;
    }

    private static string Sugerir(FaseInstancia fase) => fase switch
    {
        FaseInstancia.Jari => "Presidente da JARI",
        FaseInstancia.Cetran => "Presidente do CETRAN",
        _ => string.Empty
    };

    private static string? Limpar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    partial void OnResultadoChanged(string? valor)
    {
        OnPropertyChanged(nameof(Indeferida));
        OnPropertyChanged(nameof(Deferida));
        OnPropertyChanged(nameof(Anulada));
        _aoMudar();
    }

    partial void OnMotivoChanged(string valor) => OnPropertyChanged(nameof(TemMotivo));
    partial void OnBloqueadaChanged(bool valor) => OnPropertyChanged(nameof(Liberada));
}

/// <summary>Corrige um serviço contratado e lança o andamento dele de uma vez.</summary>
/// <remarks>
/// Duas necessidades na mesma janela, e as duas vêm do mesmo lugar: o recurso que chega
/// pronto. Corrigir, porque o que foi digitado na pressa do balcão sai errado; e lançar
/// as três fases juntas, porque um processo que já passou pela autuação e pela JARI
/// exigia seis janelas em sequência para entrar no sistema.
///
/// O caminho de sempre — protocolar, esperar, registrar — continua nos botões do cartão.
/// Este é o atalho, não o substituto.
///
/// Dinheiro não se mexe aqui: valor é assunto do Financeiro.
/// </remarks>
public partial class ServicoFormularioViewModel : ViewModelBase
{
    private readonly ContratacaoDAO _contratacoes = new();
    private readonly TipoServicoDAO _tiposServico = new();
    private readonly InstanciaDAO _instancias = new();
    private readonly MultaDAO _multas = new();
    private readonly ProcessoCnhDAO _processos = new();

    private readonly Action? _aoCancelar;
    private readonly Action? _aoSalvar;
    private readonly ServicoDaFicha _dados;

    private Multa? _multa;
    private ProcessoCnh? _processo;

    public ServicoFormularioViewModel(
        ServicoDaFichaViewModel servico,
        Action? aoCancelar = null,
        Action? aoSalvar = null)
    {
        _dados = servico.Dados;
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;

        Referencia = servico.Titulo;
        ContratadoEm = servico.ContratadoEm;
        PorInstancias = servico.PorInstancias;
        Encerrado = servico.Encerrado;

        foreach (var fase in new[] { FaseInstancia.Autuacao, FaseInstancia.Jari, FaseInstancia.Cetran })
        {
            Fases.Add(new FaseEditavelViewModel(
                fase,
                _dados.Instancias.FirstOrDefault(i => i.Fase == fase),
                RevisarProgressao));
        }

        RevisarProgressao();
        CarregarCommand.Execute(null);
    }

    public string Referencia { get; }
    public string ContratadoEm { get; }

    /// <summary>Recurso: tem as três fases para lançar. Serviço direto não tem.</summary>
    public bool PorInstancias { get; }
    public bool Encerrado { get; }

    public ObservableCollection<FaseEditavelViewModel> Fases { get; } = [];

    public IReadOnlyList<string> TiposDeProcesso { get; } = ["Suspensão", "Cassação"];
    public IReadOnlyList<string> Gravidades { get; } = ["Leve", "Média", "Grave", "Gravíssima"];

    // ---------- serviço ----------
    [ObservableProperty] public partial IReadOnlyList<TipoServico> Servicos { get; set; } = [];
    [ObservableProperty] public partial TipoServico? Servico { get; set; }
    [ObservableProperty] public partial string Observacoes { get; set; } = string.Empty;

    // ---------- multa (quando o serviço trata de uma) ----------
    [ObservableProperty] public partial string Ait { get; set; } = string.Empty;
    [ObservableProperty] public partial string Enquadramento { get; set; } = string.Empty;
    [ObservableProperty] public partial string DescricaoMulta { get; set; } = string.Empty;
    [ObservableProperty] public partial string LocalMulta { get; set; } = string.Empty;
    [ObservableProperty] public partial string DataMulta { get; set; } = string.Empty;
    [ObservableProperty] public partial string? GravidadeMulta { get; set; }
    [ObservableProperty] public partial string PrazoDefesa { get; set; } = string.Empty;
    [ObservableProperty] public partial string OrgaoAutuador { get; set; } = string.Empty;

    // ---------- processo de CNH ----------
    [ObservableProperty] public partial string NumeroProcesso { get; set; } = string.Empty;
    [ObservableProperty] public partial string? TipoDeProcesso { get; set; }
    [ObservableProperty] public partial string DataNotificacao { get; set; } = string.Empty;
    [ObservableProperty] public partial string PrazoDoProcesso { get; set; } = string.Empty;
    [ObservableProperty] public partial string OrgaoInstaurador { get; set; } = string.Empty;
    [ObservableProperty] public partial string Portaria { get; set; } = string.Empty;

    [ObservableProperty] public partial bool Carregando { get; set; } = true;
    [ObservableProperty] public partial bool Salvando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }

    public bool TemMulta => _dados.MultaId is > 0;
    public bool TemProcesso => _dados.ProcessoCnhId is > 0;
    public bool TemErro => Erro is not null;
    public bool Pronto => !Carregando;

    public string AvisoEncerrado =>
        "Serviço encerrado. As correções são registradas, mas não reabrem o andamento.";

    [RelayCommand]
    private async Task Carregar()
    {
        try
        {
            var tabela = await _tiposServico.ListarAsync();
            var atual = tabela.FirstOrDefault(s => s.Nome == _dados.Servico);

            // Só as variantes do mesmo caminho: trocar um recurso de multa por uma
            // renovação de CNH não é correção, é outro serviço.
            Servicos = atual is null
                ? tabela
                : [.. tabela.Where(s => s.Fluxo == atual.Fluxo).OrderBy(s => s.ValorTabela)];
            Servico = atual;

            if (_dados.MultaId is { } multaId)
            {
                _multa = await _multas.BuscarPorIdAsync(multaId);
                PreencherMulta();
            }

            if (_dados.ProcessoCnhId is { } processoId)
            {
                _processo = await _processos.BuscarPorIdAsync(processoId);
                PreencherProcesso();
            }
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível abrir o serviço: {excecao.Message}";
        }
        finally
        {
            Carregando = false;
            OnPropertyChanged(nameof(Pronto));
        }
    }

    private void PreencherMulta()
    {
        if (_multa is null)
        {
            return;
        }

        Ait = _multa.Ait;
        Enquadramento = _multa.Enquadramento ?? string.Empty;
        DescricaoMulta = _multa.Descricao ?? string.Empty;
        LocalMulta = _multa.LocalInfracao ?? string.Empty;
        DataMulta = Formato.Data(_multa.DataHoraInfracao);
        PrazoDefesa = Formato.Data(_multa.PrazoDefesa);
        OrgaoAutuador = _multa.OrgaoAutuador ?? string.Empty;
        GravidadeMulta = _multa.Gravidade switch
        {
            Core.Models.GravidadeMulta.Leve => "Leve",
            Core.Models.GravidadeMulta.Media => "Média",
            Core.Models.GravidadeMulta.Grave => "Grave",
            Core.Models.GravidadeMulta.Gravissima => "Gravíssima",
            _ => null
        };
    }

    private void PreencherProcesso()
    {
        if (_processo is null)
        {
            return;
        }

        NumeroProcesso = _processo.NumeroProcesso;
        TipoDeProcesso = _processo.Tipo == TipoProcessoCnh.Cassacao ? "Cassação" : "Suspensão";
        DataNotificacao = Formato.Data(_processo.DataNotificacao);
        PrazoDoProcesso = Formato.Data(_processo.PrazoDefesa);
        OrgaoInstaurador = _processo.OrgaoInstaurador ?? string.Empty;
        Portaria = _processo.Portaria ?? string.Empty;
    }

    /// <summary>Fecha as fases que uma decisão anterior tornou impossíveis.</summary>
    /// <remarks>
    /// Deferido encerra o recurso: não há JARI depois de ganhar na autuação. A fase
    /// seguinte abre quando a anterior indefere — ou quando ela é anulada, que é o caso de
    /// a fase cair sem julgamento e o processo seguir assim mesmo.
    /// </remarks>
    private void RevisarProgressao()
    {
        var encerrouAntes = false;
        var abriu = true;

        foreach (var fase in Fases)
        {
            fase.Bloqueada = encerrouAntes || !abriu;
            fase.Motivo = encerrouAntes
                ? "O recurso foi deferido antes desta fase."
                : abriu ? string.Empty : "Abre quando a fase anterior indeferir ou for anulada.";

            encerrouAntes = encerrouAntes || fase.Deferida;
            abriu = fase.Indeferida || fase.Anulada;
        }
    }

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
            await _contratacoes.CorrigirItemAsync(
                _dados.ItemId,
                Servico!.Id,
                string.IsNullOrWhiteSpace(Observacoes) ? null : Observacoes.Trim());

            if (_multa is not null)
            {
                await _multas.AtualizarAsync(MontarMulta());
            }

            if (_processo is not null)
            {
                await _processos.AtualizarAsync(MontarProcesso());
            }

            if (PorInstancias)
            {
                await _instancias.SalvarAndamentoAsync(
                    _dados.ItemId,
                    [.. Fases.Where(f => f.Liberada).Select(f => f.Montar())]);
            }

            _aoSalvar?.Invoke();
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

    private Multa MontarMulta()
    {
        var multa = _multa!;

        multa.Ait = Ait.Trim().ToUpperInvariant();
        multa.Enquadramento = Limpar(Enquadramento);
        multa.Descricao = Limpar(DescricaoMulta);
        multa.LocalInfracao = Limpar(LocalMulta);
        multa.OrgaoAutuador = Limpar(OrgaoAutuador);
        multa.PrazoDefesa = Formato.LerData(PrazoDefesa);
        multa.Gravidade = GravidadeMulta switch
        {
            "Leve" => Core.Models.GravidadeMulta.Leve,
            "Média" => Core.Models.GravidadeMulta.Media,
            "Grave" => Core.Models.GravidadeMulta.Grave,
            "Gravíssima" => Core.Models.GravidadeMulta.Gravissima,
            _ => null
        };

        // A hora da infração fica como estava: ela não aparece nesta tela, e sobrescrever
        // com meia-noite apagaria um dado que sustenta o recurso.
        if (Formato.LerData(DataMulta) is { } dia)
        {
            var hora = multa.DataHoraInfracao?.TimeOfDay ?? TimeSpan.Zero;
            multa.DataHoraInfracao = dia.Add(hora);
        }
        else
        {
            multa.DataHoraInfracao = null;
        }

        return multa;
    }

    private ProcessoCnh MontarProcesso()
    {
        var processo = _processo!;

        processo.NumeroProcesso = NumeroProcesso.Trim().ToUpperInvariant();
        processo.Tipo = TipoDeProcesso == "Cassação" ? TipoProcessoCnh.Cassacao : TipoProcessoCnh.Suspensao;
        processo.DataNotificacao = Formato.LerData(DataNotificacao);
        processo.PrazoDefesa = Formato.LerData(PrazoDoProcesso);
        processo.OrgaoInstaurador = Limpar(OrgaoInstaurador);
        processo.Portaria = Limpar(Portaria);

        return processo;
    }

    private bool Conferir()
    {
        Erro = null;

        if (Servico is null)
        {
            Erro = "Escolha o serviço.";
            return false;
        }

        if (TemMulta && Ait.Trim().Length == 0)
        {
            Erro = "O AIT é obrigatório.";
            return false;
        }

        if (TemProcesso && NumeroProcesso.Trim().Length == 0)
        {
            Erro = "O número do processo é obrigatório.";
            return false;
        }

        foreach (var fase in Fases.Where(f => f.Liberada))
        {
            if (fase.Conferir() is { } problema)
            {
                Erro = problema;
                return false;
            }
        }

        return true;
    }

    private static string? Limpar(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    partial void OnErroChanged(string? valor) => OnPropertyChanged(nameof(TemErro));
    partial void OnCarregandoChanged(bool valor) => OnPropertyChanged(nameof(Pronto));
}
