using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Marca ou altera um compromisso da agenda (RF-28), que pode se repetir (RF-31).</summary>
/// <remarks>
/// <b>Prazo não entra aqui.</b> O prazo de protocolo nasce da instância do recurso e é
/// calculado a partir dela; deixar marcar um "prazo" à mão criaria uma data que o resto
/// do sistema não conhece, concorrendo com a verdadeira na mesma tela.
///
/// O cliente é opcional: "imprimir e-mails" não é de ninguém.
///
/// Ao <b>alterar</b>, o ritmo da repetição fica travado: mudar "toda semana" para "todo
/// mês" numa ocorrência não diria o que fazer com as outras já gravadas. Trocar o ritmo é
/// apagar a série e marcar de novo.
/// </remarks>
public partial class CompromissoFormularioViewModel : ViewModelBase
{
    private readonly AgendaDAO _agenda = new();
    private readonly Action? _aoCancelar;
    private readonly Action? _aoSalvar;

    /// <summary>O item sendo alterado. Nulo quando se está marcando um novo.</summary>
    private readonly ItemDaAgenda? _emEdicao;

    private List<EscolhaDeClienteViewModel> _todosOsClientes = [];

    /// <summary>Novo compromisso, no dia escolhido na agenda.</summary>
    public CompromissoFormularioViewModel(
        DateTime dia,
        Action? aoCancelar = null,
        Action? aoSalvar = null)
    {
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;

        Data = Formato.Data(dia);

        // Um ano à frente: longe o bastante para a agenda mostrar carga futura de verdade,
        // perto o bastante para a lista não virar um muro de linhas até 2040.
        RepeteAte = Formato.Data(dia.AddYears(1));

        CarregarClientesCommand.Execute(null);
    }

    /// <summary>Alteração de um compromisso que já existe.</summary>
    public CompromissoFormularioViewModel(
        ItemDaAgenda item,
        Action? aoCancelar = null,
        Action? aoSalvar = null)
    {
        _aoCancelar = aoCancelar;
        _aoSalvar = aoSalvar;
        _emEdicao = item;

        Data = Formato.Data(item.Data);
        RepeteAte = Formato.Data(item.Data.AddYears(1));
        Hora = item.Hora ?? string.Empty;
        Titulo = item.Titulo;
        RecorrenciaEscolhida = TextoDaRecorrencia(item.Recorrencia);
        Observacao = item.Observacao ?? string.Empty;
        TipoOutro = item.TipoOutro ?? string.Empty;

        // Marcação antiga de DETRAN: a opção saiu da lista, mas a lista precisa conter
        // o valor para a caixa conseguir mostrá-lo em vez de abrir vazia.
        if (item.Tipo == TipoCompromisso.Detran)
        {
            Tipos.Add("DETRAN");
        }

        TipoEscolhido = TextoDoTipo(item.Tipo);

        CarregarClientesCommand.Execute(null);
    }

    public ObservableCollection<EscolhaDeClienteViewModel> Clientes { get; } = [];

    /// <summary>Os tipos que se marca à mão. "Prazo" fica de fora de propósito.</summary>
    public ObservableCollection<string> Tipos { get; } =
    [
        "Atendimento", "Prova", "Delegacia", "Vistoria", "Tarefa", "Outro"
    ];

    public ObservableCollection<string> Recorrencias { get; } =
    [
        "Não repete", "Toda semana", "A cada 15 dias", "Todo mês"
    ];

    [ObservableProperty] public partial string Data { get; set; }
    [ObservableProperty] public partial string RepeteAte { get; set; }
    [ObservableProperty] public partial string Hora { get; set; } = string.Empty;
    [ObservableProperty] public partial string Titulo { get; set; } = string.Empty;
    [ObservableProperty] public partial string TipoEscolhido { get; set; } = "Atendimento";
    [ObservableProperty] public partial string RecorrenciaEscolhida { get; set; } = "Não repete";
    [ObservableProperty] public partial string Observacao { get; set; } = string.Empty;

    /// <summary>Que espécie de compromisso é, quando o tipo escolhido foi "Outro".</summary>
    [ObservableProperty] public partial string TipoOutro { get; set; } = string.Empty;

    [ObservableProperty] public partial string TermoBusca { get; set; } = string.Empty;
    [ObservableProperty] public partial EscolhaDeClienteViewModel? Cliente { get; set; }

    [ObservableProperty] public partial bool EscolhendoCliente { get; set; }

    [ObservableProperty] public partial bool Salvando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? ErroTitulo { get; set; }
    [ObservableProperty] public partial string? ErroData { get; set; }
    [ObservableProperty] public partial string? ErroHora { get; set; }
    [ObservableProperty] public partial string? ErroRepeteAte { get; set; }
    [ObservableProperty] public partial string? ErroTipoOutro { get; set; }

    public bool Editando => _emEdicao is not null;

    public string TituloDaJanela => Editando ? "Alterar compromisso" : "Marcar compromisso";
    public string TextoDoBotao => Editando ? "Salvar" : "Marcar";

    public string Explicacao => Editando
        ? "Dia, hora, tipo e o resto podem mudar. O ritmo da repetição, não: para trocá-lo, apague a série e marque de novo."
        : "Atendimento, prova, ida à delegacia, vistoria ou tarefa. O prazo de protocolo não se marca aqui: ele vem do próprio recurso.";

    public bool TemErro => Erro is not null;
    public bool TemCliente => Cliente is not null;
    public bool SemClientes => Clientes.Count == 0;

    public bool Repete => RecorrenciaEscolhida != "Não repete";

    /// <summary>"Outro" sozinho não diz nada: pede o nome da espécie.</summary>
    public bool PedeOTipo => TipoEscolhido == "Outro";

    /// <summary>Alterando, o ritmo é só informação: não se mexe nele por uma ocorrência.</summary>
    public bool PodeTrocarRitmo => !Editando;

    /// <summary>Ao marcar, repetir pede até quando. Ao alterar, a série já existe.</summary>
    public bool PedeAteQuando => Repete && !Editando;

    /// <summary>Quantas vezes a tarefa vai cair, para a pessoa ver antes de gravar.</summary>
    public string Previsao
    {
        get
        {
            if (!PedeAteQuando)
            {
                return string.Empty;
            }

            var inicio = Formato.LerData(Data);
            var fim = Formato.LerData(RepeteAte);

            if (inicio is null || fim is null || fim < inicio)
            {
                return string.Empty;
            }

            var quantas = AgendaDAO.Datas(inicio.Value, fim.Value, RecorrenciaDoTexto(RecorrenciaEscolhida))
                                   .Count();

            var aviso = quantas >= AgendaDAO.LimiteDaSerie
                ? $" (o máximo de uma vez; marque de novo a partir da última para seguir adiante)"
                : string.Empty;

            return quantas == 1
                ? "Vai cair 1 vez."
                : $"Vai cair {quantas} vezes.{aviso}";
        }
    }

    partial void OnErroChanged(string? value) => OnPropertyChanged(nameof(TemErro));

    partial void OnClienteChanged(EscolhaDeClienteViewModel? value)
    {
        EscolhendoCliente = false;
        OnPropertyChanged(nameof(TemCliente));
    }

    partial void OnRecorrenciaEscolhidaChanged(string value)
    {
        OnPropertyChanged(nameof(Repete));
        OnPropertyChanged(nameof(PedeAteQuando));
        OnPropertyChanged(nameof(Previsao));
    }

    partial void OnTipoEscolhidoChanged(string value) => OnPropertyChanged(nameof(PedeOTipo));

    partial void OnDataChanged(string value) => OnPropertyChanged(nameof(Previsao));

    partial void OnRepeteAteChanged(string value) => OnPropertyChanged(nameof(Previsao));

    partial void OnTermoBuscaChanged(string value) => Filtrar();

    [RelayCommand]
    private async Task CarregarClientes()
    {
        try
        {
            var clientes = await new ClienteDAO().ListarParaEscolhaAsync();
            _todosOsClientes = [.. clientes.Select(c => new EscolhaDeClienteViewModel(c))];

            // Alterando: traz de volta o cliente que já estava no compromisso.
            if (_emEdicao?.ClienteId is { } id)
            {
                Cliente = _todosOsClientes.FirstOrDefault(c => c.Id == id);
            }

            Filtrar();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível carregar os clientes: {excecao.Message}";
        }
    }

    /// <summary>Mostra os primeiros enquanto ninguém digitou: a lista inteira é longa.</summary>
    private void Filtrar()
    {
        var termo = TermoBusca.Trim().ToLowerInvariant();

        var achados = termo.Length == 0
            ? _todosOsClientes.Take(40)
            : _todosOsClientes.Where(c => c.Busca.Contains(termo, StringComparison.Ordinal));

        Clientes.Clear();
        foreach (var cliente in achados)
        {
            Clientes.Add(cliente);
        }

        OnPropertyChanged(nameof(SemClientes));
    }

    [RelayCommand]
    private void AbrirEscolhaDeCliente() => EscolhendoCliente = true;

    [RelayCommand]
    private void Escolher(EscolhaDeClienteViewModel? cliente) => Cliente = cliente;

    [RelayCommand]
    private void TirarCliente()
    {
        Cliente = null;
        EscolhendoCliente = false;
    }

    [RelayCommand]
    private void Cancelar() => _aoCancelar?.Invoke();

    [RelayCommand]
    private async Task Salvar()
    {
        if (!Conferir(out var data, out var hora, out var ate))
        {
            return;
        }

        Salvando = true;
        Erro = null;

        try
        {
            var compromisso = new Compromisso
            {
                Id = _emEdicao?.Id ?? 0,
                Data = data,
                Hora = hora,
                Tipo = TipoDoTexto(TipoEscolhido),
                Titulo = Titulo.Trim(),
                ClienteId = Cliente?.Id,
                Concluido = _emEdicao?.Concluido ?? false,
                Recorrencia = RecorrenciaDoTexto(RecorrenciaEscolhida),
                Observacao = string.IsNullOrWhiteSpace(Observacao) ? null : Observacao.Trim(),

                // Só vale com "Outro": trocar o tipo depois deixaria para trás um
                // nome de espécie que não é mais o da marcação.
                TipoOutro = PedeOTipo ? TipoOutro.Trim() : null
            };

            if (Editando)
            {
                await _agenda.AtualizarAsync(compromisso);
                Notificacoes.Sucesso("Compromisso alterado.");
            }
            else
            {
                var quantas = await _agenda.CriarSerieAsync(compromisso, ate);

                Notificacoes.Sucesso(quantas == 1
                    ? "Compromisso marcado."
                    : $"{quantas} compromissos marcados.");
            }

            _aoSalvar?.Invoke();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível salvar o compromisso: {excecao.Message}";
        }
        finally
        {
            Salvando = false;
        }
    }

    /// <summary>Confere os campos e devolve o que o banco precisa, já convertido.</summary>
    private bool Conferir(out DateTime data, out string? hora, out DateTime ate)
    {
        data = default;
        hora = null;
        ate = default;

        ErroTitulo = string.IsNullOrWhiteSpace(Titulo) ? "Diga o que é." : null;

        ErroTipoOutro = PedeOTipo && string.IsNullOrWhiteSpace(TipoOutro)
            ? "Diga que tipo de compromisso é."
            : null;

        var lida = Formato.LerData(Data);
        ErroData = lida is null ? "Data inválida." : null;

        // Hora é opcional — tarefa do dia não tem horário —, mas se vier, vem certa.
        var texto = Hora.Trim();
        if (texto.Length > 0)
        {
            if (TimeSpan.TryParse(texto, out var horario) && horario < TimeSpan.FromDays(1))
            {
                hora = $"{horario.Hours:00}:{horario.Minutes:00}";
                ErroHora = null;
            }
            else
            {
                ErroHora = "Use hh:mm.";
            }
        }
        else
        {
            ErroHora = null;
        }

        ErroRepeteAte = null;

        if (PedeAteQuando)
        {
            var fim = Formato.LerData(RepeteAte);

            if (fim is null)
            {
                ErroRepeteAte = "Data inválida.";
            }
            else if (lida is not null && fim < lida)
            {
                ErroRepeteAte = "Não pode ser antes do primeiro dia.";
            }
            else
            {
                ate = fim.Value;
            }
        }
        else if (lida is not null)
        {
            ate = lida.Value;
        }

        if (ErroTitulo is not null || ErroData is not null || ErroHora is not null
            || ErroRepeteAte is not null || ErroTipoOutro is not null)
        {
            return false;
        }

        data = lida!.Value;
        return true;
    }

    private static TipoCompromisso TipoDoTexto(string texto) => texto switch
    {
        "Atendimento" => TipoCompromisso.Atendimento,
        "Prova" => TipoCompromisso.Prova,
        "Delegacia" => TipoCompromisso.Delegacia,
        "Vistoria" => TipoCompromisso.Vistoria,
        "Tarefa" => TipoCompromisso.Tarefa,
        _ => TipoCompromisso.Outro
    };

    private static string TextoDoTipo(TipoCompromisso tipo) => tipo switch
    {
        TipoCompromisso.Atendimento => "Atendimento",
        TipoCompromisso.Prova => "Prova",
        TipoCompromisso.Delegacia => "Delegacia",
        TipoCompromisso.Detran => "DETRAN",
        TipoCompromisso.Vistoria => "Vistoria",
        TipoCompromisso.Tarefa => "Tarefa",
        _ => "Outro"
    };

    private static Recorrencia RecorrenciaDoTexto(string texto) => texto switch
    {
        "Toda semana" => Recorrencia.Semanal,
        "A cada 15 dias" => Recorrencia.Quinzenal,
        "Todo mês" => Recorrencia.Mensal,
        _ => Recorrencia.Nenhuma
    };

    private static string TextoDaRecorrencia(Recorrencia recorrencia) => recorrencia switch
    {
        Recorrencia.Semanal => "Toda semana",
        Recorrencia.Quinzenal => "A cada 15 dias",
        Recorrencia.Mensal => "Todo mês",
        _ => "Não repete"
    };
}
