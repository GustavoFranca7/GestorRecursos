using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GerenciadorClientes.Core.Data;
using GerenciadorClientes.Core.Models;

namespace GerenciadorClientes.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    // Mesmo DAO usado pelo ConsoleApp, vindo da camada Core.
    private readonly ClienteDAO _clienteDao = new();

    // Clientes da pagina atual, cada um com o estado da setinha do endereco.
    public ObservableCollection<ClienteItemViewModel> Clientes { get; } = new();

    // Botoes numerados da paginacao (no maximo 3 por vez, entre o << e o >>).
    public ObservableCollection<PaginaBotao> NumerosPagina { get; } = new();

    // Opcoes da lista "por pagina": 10, 20, ..., 100.
    public int[] TamanhosPagina { get; } = Enumerable.Range(1, 10).Select(n => n * 10).ToArray();

    // Texto do rodape: pagina atual e intervalo mostrado, ou mensagem de erro.
    [ObservableProperty]
    public partial string Status { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TamanhoPagina { get; set; } = 10;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PaginaAnteriorCommand))]
    [NotifyCanExecuteChangedFor(nameof(ProximaPaginaCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrimeiraPaginaCommand))]
    [NotifyCanExecuteChangedFor(nameof(UltimaPaginaCommand))]
    public partial int PaginaAtual { get; set; } = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ProximaPaginaCommand))]
    [NotifyCanExecuteChangedFor(nameof(UltimaPaginaCommand))]
    public partial int TotalPaginas { get; set; } = 1;

    // Larguras das colunas que a pessoa pode arrastar no cabecalho.
    // Ficam aqui para o cabecalho e todas as linhas usarem o mesmo valor.
    [ObservableProperty]
    public partial double LarguraNome { get; set; } = 150;

    [ObservableProperty]
    public partial double LarguraEmail { get; set; } = 200;

    [ObservableProperty]
    public partial double LarguraCidade { get; set; } = 140;

    // Formulario de novo cliente aberto por cima da lista. Nulo = fechado.
    [ObservableProperty]
    public partial NovoClienteViewModel? NovoCliente { get; set; }

    // Aviso rapido do canto inferior esquerdo. Nulo = nenhum aviso na tela.
    [ObservableProperty]
    public partial string? Notificacao { get; set; }

    public MainViewModel()
    {
        // O preview da IDE cria este ViewModel para desenhar a tela. Sem esta
        // guarda, ele tentaria conectar no Oracle a cada edicao do XAML.
        if (Design.IsDesignMode)
        {
            CarregarExemploParaPreview();
            return;
        }

        CarregarPagina(1);
    }

    // ===================== Paginacao =====================

    // Botao "A": recarrega a pagina atual.
    [RelayCommand]
    private void CarregarClientes() => CarregarPagina(PaginaAtual);

    [RelayCommand(CanExecute = nameof(TemPaginaAnterior))]
    private void PaginaAnterior() => CarregarPagina(PaginaAtual - 1);

    private bool TemPaginaAnterior() => PaginaAtual > 1;

    [RelayCommand(CanExecute = nameof(TemProximaPagina))]
    private void ProximaPagina() => CarregarPagina(PaginaAtual + 1);

    private bool TemProximaPagina() => PaginaAtual < TotalPaginas;

    // Botao "<<": volta direto para a primeira pagina.
    [RelayCommand(CanExecute = nameof(TemPaginaAnterior))]
    private void PrimeiraPagina() => CarregarPagina(1);

    // Botao ">>": vai direto para a ultima pagina.
    [RelayCommand(CanExecute = nameof(TemProximaPagina))]
    private void UltimaPagina() => CarregarPagina(TotalPaginas);

    // Botoes numerados: pula direto para a pagina escolhida.
    [RelayCommand]
    private void IrParaPagina(int numero) => CarregarPagina(numero);

    // Mudou a quantidade por pagina: volta para a primeira pagina.
    partial void OnTamanhoPaginaChanged(int value) => CarregarPagina(1);

    private void CarregarPagina(int pagina)
    {
        try
        {
            int total = _clienteDao.ContarTodos();
            TotalPaginas = Math.Max(1, (int)Math.Ceiling(total / (double)TamanhoPagina));
            PaginaAtual = Math.Clamp(pagina, 1, TotalPaginas);

            Clientes.Clear();
            foreach (Cliente cliente in _clienteDao.ListarPagina(PaginaAtual, TamanhoPagina))
            {
                Clientes.Add(new ClienteItemViewModel(cliente));
            }

            AtualizarNumerosPagina();

            int primeiro = (PaginaAtual - 1) * TamanhoPagina + 1;
            int ultimo = (PaginaAtual - 1) * TamanhoPagina + Clientes.Count;
            Status = total == 0
                ? "Nenhum cliente cadastrado."
                : $"PAGINA {PaginaAtual} DE {TotalPaginas}  ·  {primeiro}-{ultimo} DE {total} CLIENTES";
        }
        catch (Exception ex)
        {
            // Sem isso, falha de conexao derrubaria a janela inteira.
            Status = $"Erro ao acessar o banco: {ex.Message}";
        }
    }

    // Janela de ate 3 numeros em volta da pagina atual. Ex.: pagina 6 de 9 -> 5 6 7.
    private void AtualizarNumerosPagina()
    {
        int inicio = Math.Clamp(PaginaAtual - 1, 1, Math.Max(1, TotalPaginas - 2));
        int fim = Math.Min(TotalPaginas, inicio + 2);

        NumerosPagina.Clear();
        for (int numero = inicio; numero <= fim; numero++)
        {
            NumerosPagina.Add(new PaginaBotao(numero, numero == PaginaAtual));
        }
    }

    // ===================== Novo cliente =====================

    // Botao "+": abre o formulario.
    [RelayCommand]
    private void AbrirNovoCliente()
    {
        NovoCliente = new NovoClienteViewModel(_clienteDao, AoSalvarNovoCliente, () => NovoCliente = null);
    }

    // Chamado pelo formulario depois de gravar: fecha, vai para a ultima pagina
    // (onde o cliente novo aparece, pela ordem de ID) e mostra o aviso.
    private void AoSalvarNovoCliente(Cliente cliente)
    {
        NovoCliente = null;
        CarregarPagina(int.MaxValue);
        Notificacao = $"Cliente {cliente.Nome} cadastrado com sucesso.";
    }

    // Dados ficticios para a tela nao aparecer vazia no preview da IDE.
    private void CarregarExemploParaPreview()
    {
        Cliente maria = new Cliente("Maria Souza", "12345678901", "maria@email.com", "47999990000")
        {
            Id = 1,
            DataCadastro = DateTime.Today,
            Endereco = new Endereco("Rua das Palmeiras", "482", "Joinville", "SC", "89201100")
        };
        Cliente joao = new Cliente("Joao Lima", "98765432100", string.Empty, "47988887777")
        {
            Id = 2,
            DataCadastro = DateTime.Today
        };

        Clientes.Add(new ClienteItemViewModel(maria) { Expandido = true });
        Clientes.Add(new ClienteItemViewModel(joao));
        AtualizarNumerosPagina();

        Status = "Pre-visualizacao com dados de exemplo.";
    }
}
