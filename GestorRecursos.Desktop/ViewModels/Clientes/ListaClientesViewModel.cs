using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>A tela de clientes: lista paginada, filtros, busca e seleção múltipla.</summary>
/// <remarks>
/// A tela não guarda a lista inteira: pede ao banco uma página por vez. Com 128 clientes
/// isso não faria diferença, mas o escritório cresce e a consulta já está pronta para isso.
/// </remarks>
public partial class ListaClientesViewModel : ViewModelBase
{
    private readonly ClienteDAO _clientes = new();
    private readonly BuscaDAO _busca = new();

    /// <summary>Evita que marcar "todos" dispare o recálculo uma vez por linha.</summary>
    private bool _ajustandoSelecao;

    /// <summary>Opções do seletor "por página", na ordem em que aparecem.</summary>
    public IReadOnlyList<int> TamanhosDePagina { get; } = [10, 25, 50, 100];

    public ObservableCollection<LinhaClienteViewModel> Linhas { get; } = [];

    /// <summary>Botões numerados da paginação, recalculados a cada carga.</summary>
    public ObservableCollection<PaginaBotao> BotoesDePagina { get; } = [];

    [ObservableProperty]
    public partial FiltroClientes Filtro { get; set; } = FiltroClientes.Ativos;

    [ObservableProperty]
    public partial int NumeroPagina { get; set; } = 1;

    [ObservableProperty]
    public partial int TamanhoPagina { get; set; } = 10;

    [ObservableProperty]
    public partial int TotalRegistros { get; set; }

    [ObservableProperty]
    public partial int TotalPaginas { get; set; }

    [ObservableProperty]
    public partial string Intervalo { get; set; } = string.Empty;

    /// <summary>O que está digitado na busca desta tela. Cada tela tem a sua.</summary>
    [ObservableProperty]
    public partial string TermoBusca { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool Carregando { get; set; }

    [ObservableProperty]
    public partial string? Erro { get; set; }

    /// <summary>Termo que gerou os resultados na tela. Vazio = listagem normal.</summary>
    [ObservableProperty]
    public partial string TermoBuscado { get; set; } = string.Empty;

    /// <summary>Quantos estão marcados agora, em toda a página.</summary>
    [ObservableProperty]
    public partial int QuantosSelecionados { get; set; }

    /// <summary>Marca a página inteira. Nulo quando só uma parte está marcada.</summary>
    [ObservableProperty]
    public partial bool? TudoSelecionado { get; set; } = false;

    /// <summary>Quantos clientes a confirmação aberta vai atingir. Zero = fechada.</summary>
    [ObservableProperty]
    public partial int QuantosNaConfirmacao { get; set; }

    /// <summary>O que o cliente leva junto se for apagado. Só no arquivo.</summary>
    [ObservableProperty]
    public partial string? AvisoDoHistorico { get; set; }

    private List<int> _idsNaConfirmacao = [];

    /// <summary>Esta lista é o arquivo, e não a listagem do dia a dia.</summary>
    /// <remarks>
    /// A mesma tela serve aos dois lugares. No arquivo não há abas de filtro, a lixeira
    /// apaga de verdade e aparece o botão de restaurar — é o único lugar onde se apaga,
    /// porque a regra desta fase é que ninguém apaga sem antes arquivar.
    /// </remarks>
    public bool ModoArquivo { get; }

    private readonly Action<int>? _aoAbrirFicha;
    private readonly Action? _aoNovoCliente;
    private readonly Action? _aoImportar;

    public ListaClientesViewModel(
        bool modoArquivo = false,
        Action<int>? aoAbrirFicha = null,
        Action? aoNovoCliente = null,
        Action? aoImportar = null)
    {
        ModoArquivo = modoArquivo;
        _aoAbrirFicha = aoAbrirFicha;
        _aoNovoCliente = aoNovoCliente;
        _aoImportar = aoImportar;

        if (modoArquivo)
        {
            Filtro = FiltroClientes.Arquivados;
        }

        // No preview do Rider não há banco nem wallet: a tela abriria com erro de conexão.
        if (!Design.IsDesignMode)
        {
            CarregarCommand.Execute(null);
        }
    }

    public bool FiltroAtivos => Filtro == FiltroClientes.Ativos;
    public bool FiltroComPendencia => Filtro == FiltroClientes.ComPendencia;
    public bool FiltroComPrazo => Filtro == FiltroClientes.ComPrazo;

    /// <summary>Abas de filtro e ações só existem na listagem do dia a dia.</summary>
    public bool MostrarCabecalho => !ModoArquivo;

    /// <summary>A dica diz onde a busca vai procurar: é a lista desta tela, não a do sistema.</summary>
    public string DicaDeBusca => ModoArquivo
        ? "Buscar no arquivo: nome, CPF, WhatsApp, placa, AIT, processo..."
        : "Buscar em clientes: nome, CPF, WhatsApp, placa, AIT, processo...";

    public bool EmBusca => !string.IsNullOrWhiteSpace(TermoBuscado);
    public string TituloDaBusca => $"Resultados para \"{TermoBuscado}\"";
    public bool TemSelecionados => QuantosSelecionados > 0;
    public string TextoSelecionados =>
        QuantosSelecionados == 1 ? "1 cliente selecionado" : $"{QuantosSelecionados} clientes selecionados";

    public bool ConfirmacaoAberta => QuantosNaConfirmacao > 0;

    public string TituloDaConfirmacao => (ModoArquivo, QuantosNaConfirmacao) switch
    {
        (true, 1) => "Apagar este cliente de vez?",
        (true, var quantos) => $"Apagar {quantos} clientes de vez?",
        (false, 1) => "Excluir este cliente?",
        (false, var quantos) => $"Excluir {quantos} clientes?"
    };

    public string TextoDaConfirmacao => ModoArquivo
        ? "Isto apaga o cadastro do banco. Não há como desfazer."
        : "Por enquanto o cliente é apenas arquivado: ele sai das listas, o histórico fica "
          + "guardado e dá para encontrá-lo em Arquivados. Quando a regra mudar, esta ação "
          + "vai apagar de verdade.";

    public string BotaoDaConfirmacao => ModoArquivo ? "Apagar de vez" : "Excluir";

    public bool TemAvisoDoHistorico => !string.IsNullOrEmpty(AvisoDoHistorico);

    public bool TemAnterior => NumeroPagina > 1 && !EmBusca;
    public bool TemProxima => NumeroPagina < TotalPaginas && !EmBusca;
    public bool ListaVazia => !Carregando && Linhas.Count == 0 && Erro is null;

    /// <summary>Lista vazia por busca e lista vazia por filtro pedem explicações diferentes.</summary>
    public string MensagemVazia => EmBusca
        ? "Nada encontrado. A placa e o AIT precisam estar inteiros; CPF e WhatsApp, só os números."
        : "Nenhum cliente neste filtro.";

    [RelayCommand]
    private async Task Carregar()
    {
        if (EmBusca)
        {
            await Buscar(TermoBuscado);
            return;
        }

        Carregando = true;
        Erro = null;

        try
        {
            var pagina = await _clientes.ListarResumoAsync(NumeroPagina, TamanhoPagina, Filtro);

            // Pediu uma página que não existe mais (arquivou a última linha, por exemplo).
            if (pagina.Itens.Count == 0 && pagina.TotalPaginas > 0 && NumeroPagina > pagina.TotalPaginas)
            {
                NumeroPagina = pagina.TotalPaginas;
                pagina = await _clientes.ListarResumoAsync(NumeroPagina, TamanhoPagina, Filtro);
            }

            TrocarLinhas(pagina.Itens);

            TotalRegistros = pagina.TotalRegistros;
            TotalPaginas = pagina.TotalPaginas;

            AtualizarIntervalo();
            MontarBotoesDePagina();
        }
        catch (Exception excecao)
        {
            MostrarErro("Não foi possível carregar os clientes", excecao);
        }
        finally
        {
            Carregando = false;
            OnPropertyChanged(nameof(ListaVazia));
        }
    }

    /// <summary>Busca global: qualquer ponta (nome, CPF, placa, AIT, processo) leva ao cliente.</summary>
    /// <summary>O que o campo de busca da tela dispara.</summary>
    [RelayCommand]
    private Task BuscarAqui() => Buscar(TermoBusca);

    /// <summary>O "+ Novo cliente" da própria tela, que só a casca sabe abrir.</summary>
    [RelayCommand]
    private void NovoCliente() => _aoNovoCliente?.Invoke();

    /// <summary>A importação em lote, ao lado do cadastro: as duas criam cliente.</summary>
    [RelayCommand]
    private void Importar() => _aoImportar?.Invoke();

    /// <summary>No arquivo não se cadastra nem se importa ninguém.</summary>
    public bool MostrarImportar => !ModoArquivo && _aoImportar is not null;

    public async Task Buscar(string termo)
    {
        if (string.IsNullOrWhiteSpace(termo))
        {
            await LimparBusca();
            return;
        }

        Carregando = true;
        Erro = null;
        TermoBuscado = termo.Trim();

        try
        {
            var achados = await _busca.ProcurarAsync(TermoBuscado);

            // Vários resultados podem apontar para o mesmo cliente (duas placas dele, por
            // exemplo). A lista mostra o cliente uma vez só, com o primeiro detalhe.
            var porCliente = achados
                .GroupBy(a => a.ClienteId)
                .ToDictionary(g => g.Key, g => g.First());

            // A busca global encontra ativos e arquivados; cada tela fica com os seus.
            var resumos = await _clientes.ListarResumoPorIdsAsync([.. porCliente.Keys], ModoArquivo);

            TrocarLinhas(resumos, porCliente);

            TotalRegistros = resumos.Count;
            TotalPaginas = 1;
            NumeroPagina = 1;
            BotoesDePagina.Clear();
            Intervalo = resumos.Count == 1 ? "1 cliente encontrado" : $"{resumos.Count} clientes encontrados";
        }
        catch (Exception excecao)
        {
            MostrarErro("Não foi possível buscar", excecao);
        }
        finally
        {
            Carregando = false;
            OnPropertyChanged(nameof(ListaVazia));
        }
    }

    [RelayCommand]
    private async Task LimparBusca()
    {
        TermoBuscado = string.Empty;
        TermoBusca = string.Empty;
        NumeroPagina = 1;
        await Carregar();
    }

    [RelayCommand]
    private async Task TrocarFiltro(FiltroClientes filtro)
    {
        if (Filtro == filtro && !EmBusca)
        {
            return;
        }

        Filtro = filtro;
        TermoBuscado = string.Empty;
        NumeroPagina = 1;
        await Carregar();
    }

    [RelayCommand]
    private async Task IrParaPagina(int numero)
    {
        if (numero < 1 || numero > TotalPaginas || numero == NumeroPagina)
        {
            return;
        }

        NumeroPagina = numero;
        await Carregar();
    }

    [RelayCommand]
    private Task Primeira() => IrParaPagina(1);

    [RelayCommand]
    private Task Anterior() => IrParaPagina(NumeroPagina - 1);

    [RelayCommand]
    private Task Proxima() => IrParaPagina(NumeroPagina + 1);

    [RelayCommand]
    private Task Ultima() => IrParaPagina(TotalPaginas);

    [RelayCommand]
    private void LimparSelecao()
    {
        TudoSelecionado = false;
        DesmarcarTodas();
    }

    /// <summary>Abre a ficha do cliente, pelo botão da pasta.</summary>
    [RelayCommand]
    private void AbrirFicha(int id) => _aoAbrirFicha?.Invoke(id);

    /// <summary>A pasta só funciona na listagem: no arquivo não há ficha para abrir.</summary>
    public bool PodeAbrirFicha => _aoAbrirFicha is not null;

    /// <summary>Pede confirmação pelo botão da lixeira, para um cliente só.</summary>
    [RelayCommand]
    private Task PedirExclusao(int id) => AbrirConfirmacao([id]);

    /// <summary>Pede confirmação para os que estão marcados.</summary>
    [RelayCommand]
    private Task PedirExclusaoDosSelecionados() =>
        AbrirConfirmacao([.. Linhas.Where(l => l.Selecionado).Select(l => l.Id)]);

    /// <summary>Tira o cliente do arquivo e devolve às listas.</summary>
    /// <remarks>
    /// É a outra metade do arquivamento: sem um caminho de volta, "excluir por engano"
    /// continuaria sendo irreversível na prática, que é justamente o que arquivar evita.
    /// </remarks>
    [RelayCommand]
    private async Task Restaurar(int id)
    {
        try
        {
            await _clientes.ArquivarAsync(id, arquivar: false);
            await Carregar();
        }
        catch (Exception excecao)
        {
            MostrarErro("Não foi possível restaurar", excecao);
        }
    }

    /// <summary>Devolve às listas todos os que estão marcados.</summary>
    [RelayCommand]
    private async Task RestaurarSelecionados()
    {
        var ids = Linhas.Where(l => l.Selecionado).Select(l => l.Id).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        try
        {
            await _clientes.ArquivarVariosAsync(ids, arquivar: false);
            await Carregar();
        }
        catch (Exception excecao)
        {
            MostrarErro("Não foi possível restaurar", excecao);
        }
    }

    [RelayCommand]
    private void CancelarExclusao()
    {
        _idsNaConfirmacao = [];
        QuantosNaConfirmacao = 0;
        AvisoDoHistorico = null;
    }

    /// <summary>Executa o que a confirmação prometeu: arquivar na listagem, apagar no arquivo.</summary>
    [RelayCommand]
    private async Task ConfirmarExclusao()
    {
        var ids = _idsNaConfirmacao;
        _idsNaConfirmacao = [];
        QuantosNaConfirmacao = 0;
        AvisoDoHistorico = null;

        if (ids.Count == 0)
        {
            return;
        }

        try
        {
            if (ModoArquivo)
            {
                foreach (var id in ids)
                {
                    await _clientes.ExcluirDefinitivoAsync(id);
                }
            }
            else
            {
                await _clientes.ArquivarVariosAsync(ids);
            }

            await Carregar();
        }
        catch (Exception excecao)
        {
            MostrarErro(ModoArquivo ? "Não foi possível apagar" : "Não foi possível excluir", excecao);
        }
    }

    /// <summary>Abre a confirmação e, no arquivo, soma o que será apagado junto.</summary>
    private async Task AbrirConfirmacao(List<int> ids)
    {
        if (ids.Count == 0)
        {
            return;
        }

        _idsNaConfirmacao = ids;
        QuantosNaConfirmacao = ids.Count;
        AvisoDoHistorico = ModoArquivo ? await ResumirOQueSai(ids) : null;
    }

    /// <summary>Conta contratações, processos e pagamentos que somem junto com os clientes.</summary>
    private async Task<string?> ResumirOQueSai(List<int> ids)
    {
        try
        {
            int contratacoes = 0, processos = 0, pagamentos = 0;
            decimal totalPago = 0;

            foreach (var id in ids)
            {
                if (await _clientes.ResumirExclusaoAsync(id) is not { } resumo)
                {
                    continue;
                }

                contratacoes += resumo.Contratacoes;
                processos += resumo.Processos;
                pagamentos += resumo.Pagamentos;
                totalPago += resumo.TotalPago;
            }

            if (contratacoes == 0 && processos == 0 && pagamentos == 0)
            {
                return null;
            }

            var partes = new List<string>();
            if (contratacoes > 0) partes.Add(contratacoes == 1 ? "1 contratação" : $"{contratacoes} contratações");
            if (processos > 0) partes.Add(processos == 1 ? "1 processo" : $"{processos} processos");
            if (pagamentos > 0)
            {
                var texto = pagamentos == 1 ? "1 pagamento" : $"{pagamentos} pagamentos";
                partes.Add($"{texto} somando {Formato.Dinheiro(totalPago)}");
            }

            return $"Vai junto: {string.Join(", ", partes)}.";
        }
        catch
        {
            // O aviso é um extra; se a contagem falhar, a confirmação continua valendo.
            return null;
        }
    }

    partial void OnTamanhoPaginaChanged(int valor)
    {
        NumeroPagina = 1;
        CarregarCommand.Execute(null);
    }

    partial void OnFiltroChanged(FiltroClientes valor)
    {
        OnPropertyChanged(nameof(FiltroAtivos));
        OnPropertyChanged(nameof(FiltroComPendencia));
        OnPropertyChanged(nameof(FiltroComPrazo));
    }

    partial void OnNumeroPaginaChanged(int valor) => AvisarPaginacao();

    partial void OnTotalPaginasChanged(int valor) => AvisarPaginacao();

    partial void OnTermoBuscadoChanged(string valor)
    {
        OnPropertyChanged(nameof(EmBusca));
        OnPropertyChanged(nameof(TituloDaBusca));
        OnPropertyChanged(nameof(MensagemVazia));
        AvisarPaginacao();
    }

    partial void OnQuantosSelecionadosChanged(int valor)
    {
        OnPropertyChanged(nameof(TemSelecionados));
        OnPropertyChanged(nameof(TextoSelecionados));
    }

    partial void OnQuantosNaConfirmacaoChanged(int valor)
    {
        OnPropertyChanged(nameof(ConfirmacaoAberta));
        OnPropertyChanged(nameof(TituloDaConfirmacao));
    }

    partial void OnAvisoDoHistoricoChanged(string? valor) =>
        OnPropertyChanged(nameof(TemAvisoDoHistorico));

    partial void OnTudoSelecionadoChanged(bool? valor)
    {
        // Nulo é o estado "parcial", que a própria tela produz: não mexe nas linhas.
        if (_ajustandoSelecao || valor is not { } marcar)
        {
            return;
        }

        _ajustandoSelecao = true;
        foreach (var linha in Linhas)
        {
            linha.Selecionado = marcar;
        }

        _ajustandoSelecao = false;
        ContarSelecionados();
    }

    /// <summary>Troca o conteúdo da lista, religando o aviso de seleção de cada linha.</summary>
    private void TrocarLinhas(
        IEnumerable<ResumoCliente> resumos,
        IReadOnlyDictionary<int, ResultadoBusca>? contextos = null)
    {
        foreach (var antiga in Linhas)
        {
            antiga.PropertyChanged -= LinhaMudou;
        }

        Linhas.Clear();

        foreach (var resumo in resumos)
        {
            var linha = new LinhaClienteViewModel(resumo);

            if (contextos is not null
                && contextos.TryGetValue(resumo.Id, out var achado)
                && achado.Origem != "CLIENTE")
            {
                linha.Contexto = achado.Detalhe;
            }

            linha.PropertyChanged += LinhaMudou;
            Linhas.Add(linha);
        }

        _ajustandoSelecao = true;
        TudoSelecionado = false;
        _ajustandoSelecao = false;
        ContarSelecionados();
    }

    private void LinhaMudou(object? remetente, PropertyChangedEventArgs argumentos)
    {
        if (argumentos.PropertyName == nameof(LinhaClienteViewModel.Selecionado) && !_ajustandoSelecao)
        {
            ContarSelecionados();
        }
    }

    private void ContarSelecionados()
    {
        QuantosSelecionados = Linhas.Count(l => l.Selecionado);

        // O checkbox do cabeçalho vira "parcial" sozinho, sem mexer nas linhas de volta.
        _ajustandoSelecao = true;
        TudoSelecionado = QuantosSelecionados switch
        {
            0 => false,
            var quantos when quantos == Linhas.Count => true,
            _ => null
        };
        _ajustandoSelecao = false;
    }

    private void DesmarcarTodas()
    {
        _ajustandoSelecao = true;
        foreach (var linha in Linhas)
        {
            linha.Selecionado = false;
        }

        _ajustandoSelecao = false;
        ContarSelecionados();
    }

    private void MostrarErro(string oQueFalhou, Exception excecao)
    {
        Erro = $"{oQueFalhou}: {excecao.Message}";
        Linhas.Clear();
        TotalRegistros = 0;
        TotalPaginas = 0;
        BotoesDePagina.Clear();
        QuantosSelecionados = 0;
    }

    private void AvisarPaginacao()
    {
        OnPropertyChanged(nameof(TemAnterior));
        OnPropertyChanged(nameof(TemProxima));
    }

    private void AtualizarIntervalo()
    {
        if (TotalRegistros == 0)
        {
            Intervalo = "Nenhum cliente";
            return;
        }

        var primeiro = ((NumeroPagina - 1) * TamanhoPagina) + 1;
        var ultimo = Math.Min(primeiro + Linhas.Count - 1, TotalRegistros);
        Intervalo = $"Mostrando {primeiro}–{ultimo} de {TotalRegistros}";
    }

    /// <summary>Mostra no máximo cinco números, com a página atual no meio da janela.</summary>
    private void MontarBotoesDePagina()
    {
        BotoesDePagina.Clear();

        if (TotalPaginas <= 1)
        {
            return;
        }

        const int quantos = 5;
        var inicio = Math.Max(1, NumeroPagina - (quantos / 2));
        var fim = Math.Min(TotalPaginas, inicio + quantos - 1);
        inicio = Math.Max(1, fim - quantos + 1);

        for (var numero = inicio; numero <= fim; numero++)
        {
            BotoesDePagina.Add(new PaginaBotao(numero, numero == NumeroPagina));
        }
    }
}
