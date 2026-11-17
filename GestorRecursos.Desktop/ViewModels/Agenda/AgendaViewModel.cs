using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>As duas formas de olhar o mesmo mês.</summary>
public enum VisaoAgenda
{
    /// <summary>O mês em grade, com o dia escolhido ao lado (RF-25).</summary>
    Calendario,

    /// <summary>Os mesmos itens em linhas, agrupados por dia (RF-26).</summary>
    Lista
}

/// <summary>A seção Agenda: o mês, o dia e o que foi feito nele.</summary>
/// <remarks>
/// Substitui a planilha de produção diária. O mês é a visão que abre porque a pergunta
/// é de carga futura — "às vezes tem 1 dia com 15 recursos" —, e isso só aparece olhando
/// o mês inteiro; a lista existe ao lado para quem quer ler os dias em frases, com o AIT
/// e o serviço inteiros, em vez de contar etiquetas em quadrados.
///
/// O painel do dia fica fixo à direita de propósito: o plano (RF-29) e o diário (RF-30)
/// são o que substitui a planilha, e atrás de um clique viram coisa que se preenche
/// "quando sobrar tempo" — que é como a planilha foi ficando para trás.
///
/// <b>Duas fontes, uma tela.</b> Prazo vem das INSTANCIAS e compromisso vem da AGENDA.
/// O prazo não se fecha aqui: protocolar é um ato com data e número, que se registra na
/// ficha. Clicar num prazo leva para lá.
/// </remarks>
public partial class AgendaViewModel : ViewModelBase
{
    private static readonly CultureInfo Brasil = new("pt-BR");

    private readonly AgendaDAO _agenda = new();
    private readonly Action<int>? _aoAbrirCliente;

    private List<ItemDaAgendaViewModel> _itensDoMes = [];
    private Dictionary<DateTime, string> _diarios = [];

    /// <summary>O texto do diário como está no banco, para saber se mudou.</summary>
    private string _diarioGravado = string.Empty;

    public ObservableCollection<DiaDoMesViewModel> Dias { get; } = [];
    public ObservableCollection<ItemDaAgendaViewModel> ItensDoDia { get; } = [];
    public ObservableCollection<DiaDaListaViewModel> DiasDaLista { get; } = [];
    public ObservableCollection<OpcaoDeMes> MesesDoSeletor { get; } = [];

    public AgendaViewModel(Action<int>? aoAbrirCliente = null)
    {
        _aoAbrirCliente = aoAbrirCliente;

        var hoje = DateTime.Today;
        Ano = hoje.Year;
        Mes = hoje.Month;
        DiaEscolhido = hoje;

        // No preview do Rider não há banco nem wallet: a tela abriria com erro de conexão.
        if (!Design.IsDesignMode)
        {
            CarregarCommand.Execute(null);
        }
    }

    [ObservableProperty]
    public partial VisaoAgenda Visao { get; set; } = VisaoAgenda.Calendario;

    public bool NoCalendario => Visao == VisaoAgenda.Calendario;
    public bool NaLista => Visao == VisaoAgenda.Lista;

    public int Ano { get; private set; }
    public int Mes { get; private set; }

    [ObservableProperty]
    public partial string TituloDoMes { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime DiaEscolhido { get; set; }

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? Erro { get; set; }

    /// <summary>O formulário de compromisso, por cima da tela. Nulo quando não há.</summary>
    [ObservableProperty]
    public partial ViewModelBase? Formulario { get; set; }

    // ---------------------------------------------------------------------
    // O mês
    // ---------------------------------------------------------------------

    /// <summary>O seletor de mês, aberto por cima do título.</summary>
    [ObservableProperty]
    public partial bool SeletorAberto { get; set; }

    [ObservableProperty]
    public partial int AnoDoSeletor { get; set; }

    /// <summary>
    /// A agenda anda para a frente sem limite, ao contrário do caixa: prazo de fevereiro
    /// já existe hoje, e é justamente o mês que ainda não chegou que se quer ver.
    /// </summary>
    public bool NoMesAtual => Ano == DateTime.Today.Year && Mes == DateTime.Today.Month;

    // ---------------------------------------------------------------------
    // O painel do dia
    // ---------------------------------------------------------------------

    [ObservableProperty]
    public partial string TituloDoDia { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ResumoDoDia { get; set; } = string.Empty;

    /// <summary>O diário, editável. Só vai ao banco quando se manda gravar.</summary>
    [ObservableProperty]
    public partial string Diario { get; set; } = string.Empty;

    public bool DiarioMudou => Diario.Trim() != _diarioGravado;

    [ObservableProperty]
    public partial int PrazosFeitos { get; set; }

    [ObservableProperty]
    public partial int PrazosDoDia { get; set; }

    public bool TemPlano => PrazosDoDia > 0;

    public string PlanoTexto => PrazosDoDia == 1 ? "de 1 recurso" : $"de {PrazosDoDia} recursos";

    /// <summary>A barra do plano, sobre um trilho de 300 pixels.</summary>
    public double LarguraDoPlano => PrazosDoDia == 0 ? 0 : (double)PrazosFeitos / PrazosDoDia * 300;

    /// <summary>Dia sem nada marcado à mão. Prazo não conta: ele tem cartão próprio.</summary>
    public bool SemCompromissosNoDia => !ItensDoDia.Any(i => i.PodeConcluir);

    /// <summary>Nada feito ainda: o zero em verde pareceria conquista.</summary>
    public string CorDoPlano => PrazosFeitos > 0 ? "Sucesso" : "TextoApagado";

    // ---------------------------------------------------------------------
    // A lista
    // ---------------------------------------------------------------------

    public const string TodosOsTipos = "Tudo";

    public ObservableCollection<string> Tipos { get; } =
    [
        TodosOsTipos, "Prazos", "Atendimentos", "Provas", "Delegacia e vistorias", "Tarefas"
    ];

    [ObservableProperty]
    public partial string TipoEscolhido { get; set; } = TodosOsTipos;

    [ObservableProperty]
    public partial string TermoBusca { get; set; } = string.Empty;

    /// <summary>Esconde o que já passou: a lista serve para o que ainda vem.</summary>
    [ObservableProperty]
    public partial bool SoDaquiPraFrente { get; set; } = true;

    public bool TemBusca => TermoBusca.Length > 0;
    public bool SemNaLista => !Carregando && DiasDaLista.Count == 0;

    [ObservableProperty]
    public partial string ResumoDaLista { get; set; } = string.Empty;

    // ---------------------------------------------------------------------
    // Carga
    // ---------------------------------------------------------------------

    [RelayCommand]
    private async Task Carregar()
    {
        Carregando = true;
        Erro = null;

        try
        {
            var mes = await _agenda.MontarMesAsync(Ano, Mes);

            _itensDoMes = [.. mes.Itens
                .OrderBy(i => i.Data)
                .ThenBy(i => i.Hora ?? string.Empty)
                .ThenBy(i => i.Titulo)
                .Select(i => new ItemDaAgendaViewModel(i))];

            _diarios = mes.Diarios.ToDictionary(d => d.Key, d => d.Value);

            TituloDoMes = MesPorExtenso(Ano, Mes);

            MontarGrade();
            MontarLista();
            MostrarDia();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível carregar a agenda: {excecao.Message}";
        }
        finally
        {
            Carregando = false;
            OnPropertyChanged(nameof(SemNaLista));
        }
    }

    /// <summary>A grade do mês, começando no domingo e fechando a última semana.</summary>
    /// <remarks>
    /// A grade mostra os dias vizinhos de outro mês em cinza em vez de deixar buracos:
    /// semana cortada pela metade engana a leitura da carga.
    /// </remarks>
    private void MontarGrade()
    {
        Dias.Clear();

        var primeiro = new DateTime(Ano, Mes, 1);
        var ultimo = primeiro.AddMonths(1).AddDays(-1);

        var inicio = primeiro.AddDays(-(int)primeiro.DayOfWeek);
        var fim = ultimo.AddDays(6 - (int)ultimo.DayOfWeek);

        var porDia = _itensDoMes
            .GroupBy(i => i.Data.Date)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ItemDaAgendaViewModel>)[.. g]);

        for (var dia = inicio; dia <= fim; dia = dia.AddDays(1))
        {
            var itens = porDia.TryGetValue(dia.Date, out var achados) ? achados : [];

            Dias.Add(new DiaDoMesViewModel(dia, dia.Month == Mes && dia.Year == Ano, itens)
            {
                Escolhido = dia.Date == DiaEscolhido.Date
            });
        }
    }

    private void MontarLista()
    {
        var itens = _itensDoMes.AsEnumerable();

        if (SoDaquiPraFrente)
        {
            itens = itens.Where(i => i.Data.Date >= DateTime.Today);
        }

        itens = TipoEscolhido switch
        {
            "Prazos" => itens.Where(i => i.Dados.Tipo == TipoCompromisso.Prazo),
            "Atendimentos" => itens.Where(i => i.Dados.Tipo == TipoCompromisso.Atendimento),
            "Provas" => itens.Where(i => i.Dados.Tipo == TipoCompromisso.Prova),
            // DETRAN entra junto: a opção saiu da tela, mas as marcações antigas ficaram.
            "Delegacia e vistorias" => itens.Where(i =>
                i.Dados.Tipo is TipoCompromisso.Delegacia
                              or TipoCompromisso.Detran
                              or TipoCompromisso.Vistoria),
            "Tarefas" => itens.Where(i => i.Dados.Tipo == TipoCompromisso.Tarefa),
            _ => itens
        };

        var termo = TermoBusca.Trim().ToLowerInvariant();

        if (termo.Length > 0)
        {
            itens = itens.Where(i => i.Busca.Contains(termo, StringComparison.Ordinal));
        }

        var lista = itens.ToList();

        DiasDaLista.Clear();
        foreach (var grupo in lista.GroupBy(i => i.Data.Date).OrderBy(g => g.Key))
        {
            DiasDaLista.Add(new DiaDaListaViewModel(grupo.Key, [.. grupo]));
        }

        var dias = DiasDaLista.Count;
        ResumoDaLista = lista.Count == 0
            ? string.Empty
            : $"{lista.Count} {(lista.Count == 1 ? "item" : "itens")} em " +
              $"{dias} {(dias == 1 ? "dia" : "dias")}";

        OnPropertyChanged(nameof(SemNaLista));
    }

    /// <summary>Carrega o painel com o dia escolhido: itens, plano e diário.</summary>
    private void MostrarDia()
    {
        TituloDoDia = CabecalhoDoDia.De(DiaEscolhido);

        ItensDoDia.Clear();
        foreach (var item in _itensDoMes.Where(i => i.Data.Date == DiaEscolhido.Date))
        {
            ItensDoDia.Add(item);
        }

        var prazos = ItensDoDia.Where(i => i.EhPrazo).ToList();
        PrazosDoDia = prazos.Count;
        PrazosFeitos = prazos.Count(p => p.Dados.Concluido);

        var compromissos = ItensDoDia.Count - prazos.Count;
        var vencendo = prazos.Count(p => p.Urgente);

        var partes = new List<string>();

        if (prazos.Count > 0)
        {
            partes.Add(prazos.Count == 1 ? "1 prazo" : $"{prazos.Count} prazos");
        }

        if (compromissos > 0)
        {
            partes.Add(compromissos == 1 ? "1 compromisso" : $"{compromissos} compromissos");
        }

        // Só aparece quando há o que correr: no dia tranquilo seria ruído.
        if (vencendo > 0)
        {
            partes.Add(vencendo == 1 ? "1 vencendo hoje ou antes" : $"{vencendo} vencendo hoje ou antes");
        }

        ResumoDoDia = partes.Count > 0 ? string.Join(" · ", partes) : "nada marcado";

        _diarioGravado = _diarios.TryGetValue(DiaEscolhido.Date, out var texto) ? texto : string.Empty;
        Diario = _diarioGravado;

        OnPropertyChanged(nameof(TemPlano));
        OnPropertyChanged(nameof(PlanoTexto));
        OnPropertyChanged(nameof(LarguraDoPlano));
        OnPropertyChanged(nameof(SemCompromissosNoDia));
        OnPropertyChanged(nameof(CorDoPlano));
    }

    // ---------------------------------------------------------------------
    // Navegação
    // ---------------------------------------------------------------------

    [RelayCommand]
    private void TrocarVisao(VisaoAgenda visao)
    {
        Visao = visao;

        OnPropertyChanged(nameof(NoCalendario));
        OnPropertyChanged(nameof(NaLista));
    }

    [RelayCommand]
    private void MesAnterior() => IrPara(-1);

    [RelayCommand]
    private void MesSeguinte() => IrPara(1);

    private void IrPara(int meses)
    {
        var destino = new DateTime(Ano, Mes, 1).AddMonths(meses);
        Ir(destino.Year, destino.Month);
    }

    /// <summary>
    /// Trocar de mês escolhe o dia 1 do destino, menos quando é o mês corrente: aí o dia
    /// é hoje. Manter o dia 12 ao ir para dezembro mostraria um dia sem motivo.
    /// </summary>
    private void Ir(int ano, int mes)
    {
        Ano = ano;
        Mes = mes;

        var hoje = DateTime.Today;
        DiaEscolhido = ano == hoje.Year && mes == hoje.Month ? hoje : new DateTime(ano, mes, 1);

        OnPropertyChanged(nameof(NoMesAtual));
        CarregarCommand.Execute(null);
    }

    [RelayCommand]
    private void AbrirSeletor()
    {
        AnoDoSeletor = Ano;
        MontarSeletor();
        SeletorAberto = true;
    }

    [RelayCommand]
    private void AnoAnterior()
    {
        AnoDoSeletor--;
        MontarSeletor();
    }

    [RelayCommand]
    private void AnoSeguinte()
    {
        AnoDoSeletor++;
        MontarSeletor();
    }

    [RelayCommand]
    private void EscolherMes(OpcaoDeMes? opcao)
    {
        if (opcao is null)
        {
            return;
        }

        SeletorAberto = false;
        Ir(AnoDoSeletor, opcao.Numero);
    }

    [RelayCommand]
    private void IrParaHoje()
    {
        SeletorAberto = false;

        var hoje = DateTime.Today;

        if (Ano == hoje.Year && Mes == hoje.Month)
        {
            // Já é o mês certo: só muda o dia escolhido, sem ir ao banco de novo.
            EscolherDia(Dias.FirstOrDefault(d => d.Data.Date == hoje));
            return;
        }

        Ir(hoje.Year, hoje.Month);
    }

    /// <summary>Todo mês é escolhível: a agenda olha para a frente.</summary>
    private void MontarSeletor()
    {
        MesesDoSeletor.Clear();

        for (var mes = 1; mes <= 12; mes++)
        {
            MesesDoSeletor.Add(new OpcaoDeMes(
                mes,
                disponivel: true,
                escolhido: AnoDoSeletor == Ano && mes == Mes));
        }
    }

    [RelayCommand]
    private void EscolherDia(DiaDoMesViewModel? dia)
    {
        if (dia is null)
        {
            return;
        }

        // Clicar num dia da borda que é de outro mês leva para aquele mês.
        if (!dia.DoMes)
        {
            DiaEscolhido = dia.Data;
            Ano = dia.Data.Year;
            Mes = dia.Data.Month;

            OnPropertyChanged(nameof(NoMesAtual));
            CarregarCommand.Execute(null);
            return;
        }

        DiaEscolhido = dia.Data;

        foreach (var outro in Dias)
        {
            outro.Escolhido = outro.Data.Date == dia.Data.Date;
        }

        MostrarDia();
    }

    // ---------------------------------------------------------------------
    // Filtros da lista
    // ---------------------------------------------------------------------

    partial void OnTipoEscolhidoChanged(string value) => MontarLista();

    partial void OnSoDaquiPraFrenteChanged(bool value) => MontarLista();

    partial void OnTermoBuscaChanged(string value)
    {
        MontarLista();
        OnPropertyChanged(nameof(TemBusca));
    }

    [RelayCommand]
    private void LimparBusca() => TermoBusca = string.Empty;

    // ---------------------------------------------------------------------
    // Ações
    // ---------------------------------------------------------------------

    /// <summary>Marca ou desmarca o compromisso daquele dia.</summary>
    /// <remarks>
    /// A ocorrência é só dela: a série inteira já está gravada desde que foi criada, e
    /// marcar a desta semana não mexe nas outras.
    /// </remarks>
    [RelayCommand]
    private async Task AlternarConcluido(ItemDaAgendaViewModel? item)
    {
        if (item is null || !item.PodeConcluir)
        {
            return;
        }

        var novo = !item.Concluido;

        // A caixinha muda na hora; se o banco recusar, a recarga traz a verdade de volta.
        item.Concluido = novo;

        try
        {
            await _agenda.ConcluirAsync(item.Id, novo);
            CarregarCommand.Execute(null);
        }
        catch (Exception excecao)
        {
            item.Concluido = !novo;
            Erro = $"Não foi possível marcar o compromisso: {excecao.Message}";
        }
    }

    [RelayCommand]
    private void NovoCompromisso() =>
        Formulario = new CompromissoFormularioViewModel(
            DiaEscolhido,
            aoCancelar: () => Formulario = null,
            aoSalvar: () =>
            {
                Formulario = null;
                CarregarCommand.Execute(null);
            });

    /// <summary>Abre o compromisso para alterar o dia, a hora ou o resto.</summary>
    [RelayCommand]
    private void EditarCompromisso(ItemDaAgendaViewModel? item)
    {
        if (item is null || item.EhPrazo)
        {
            return;
        }

        Formulario = new CompromissoFormularioViewModel(
            item.Dados,
            aoCancelar: () => Formulario = null,
            aoSalvar: () =>
            {
                Formulario = null;
                CarregarCommand.Execute(null);
            });
    }

    // ---------------------------------------------------------------------
    // Exclusão
    // ---------------------------------------------------------------------

    /// <summary>O compromisso que repete e está esperando a escolha de quanto apagar.</summary>
    /// <remarks>
    /// Compromisso de uma vez só some no clique. O que repete, não: a pessoa pode querer
    /// desmarcar o desta semana ou parar de repetir de vez, e adivinhar errado apagaria
    /// cinquenta linhas sem ter perguntado.
    /// </remarks>
    [ObservableProperty]
    public partial ItemDaAgendaViewModel? SerieAExcluir { get; set; }

    [RelayCommand]
    private async Task ExcluirCompromisso(ItemDaAgendaViewModel? item)
    {
        if (item is null || item.EhPrazo)
        {
            return;
        }

        if (item.Dados.Repete)
        {
            SerieAExcluir = item;
            return;
        }

        await ApagarUm(item);
    }

    [RelayCommand]
    private async Task ExcluirSoEste()
    {
        if (SerieAExcluir is { } item)
        {
            SerieAExcluir = null;
            await ApagarUm(item);
        }
    }

    [RelayCommand]
    private async Task ExcluirAsSeguintes()
    {
        if (SerieAExcluir is not { } item)
        {
            return;
        }

        SerieAExcluir = null;

        try
        {
            var quantas = await _agenda.ExcluirSerieAsync(new Compromisso
            {
                Data = item.Data,
                Titulo = item.Dados.Titulo,
                ClienteId = item.ClienteId,
                Recorrencia = item.Dados.Recorrencia
            });

            Notificacoes.Sucesso(quantas == 1
                ? "Compromisso excluído."
                : $"{quantas} compromissos excluídos.");

            CarregarCommand.Execute(null);
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível excluir a série: {excecao.Message}";
        }
    }

    [RelayCommand]
    private void DesistirDaExclusao() => SerieAExcluir = null;

    private async Task ApagarUm(ItemDaAgendaViewModel item)
    {
        try
        {
            await _agenda.ExcluirAsync(item.Id);
            Notificacoes.Sucesso("Compromisso excluído.");
            CarregarCommand.Execute(null);
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível excluir o compromisso: {excecao.Message}";
        }
    }

    /// <summary>Grava o diário do dia escolhido (RF-30).</summary>
    /// <remarks>
    /// Com botão, e não ao sair do campo: o diário se escreve em pedaços ao longo do dia,
    /// e gravar a cada clique fora guardaria frase pela metade sem ninguém mandar.
    /// </remarks>
    [RelayCommand]
    private async Task SalvarDiario()
    {
        try
        {
            var texto = Diario.Trim();

            await _agenda.GravarAnotacaoAsync(DiaEscolhido, texto);

            _diarioGravado = texto;
            _diarios[DiaEscolhido.Date] = texto;

            Notificacoes.Sucesso(texto.Length > 0 ? "Diário do dia salvo." : "Diário do dia apagado.");
            OnPropertyChanged(nameof(DiarioMudou));
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível salvar o diário: {excecao.Message}";
        }
    }

    partial void OnDiarioChanged(string value) => OnPropertyChanged(nameof(DiarioMudou));

    /// <summary>Leva à ficha: é lá que o protocolo do prazo se registra.</summary>
    [RelayCommand]
    private void AbrirCliente(ItemDaAgendaViewModel? item)
    {
        if (item?.ClienteId is { } id)
        {
            _aoAbrirCliente?.Invoke(id);
        }
    }

    /// <summary>"Novembro de 2026", com a primeira letra maiúscula.</summary>
    private static string MesPorExtenso(int ano, int mes)
    {
        var nome = new DateTime(ano, mes, 1).ToString("MMMM 'de' yyyy", Brasil);

        return char.ToUpper(nome[0], Brasil) + nome[1..];
    }
}
