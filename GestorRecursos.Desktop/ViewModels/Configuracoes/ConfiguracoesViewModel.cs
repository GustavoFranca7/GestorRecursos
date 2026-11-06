using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;
using GestorRecursos.Core.Seguranca;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Uma linha da lista de acessos.</summary>
public class LinhaUsuarioViewModel(Usuario dados)
{
    public Usuario Dados { get; } = dados;

    public int Id => Dados.Id;
    public string Nome => Dados.Nome;
    public string Login => $"@{Dados.Login}";
    public bool Ativo => Dados.Ativo;

    public string Situacao => Dados switch
    {
        { Ativo: false } => "desativado",
        { PrimeiroAcesso: true } => "aguardando o primeiro acesso",
        _ => "em uso"
    };

    public string FundoSelo => Dados switch
    {
        { Ativo: false } => "Fundo",
        { PrimeiroAcesso: true } => "AvisoClaro",
        _ => "SucessoClaro"
    };

    public string CorSelo => Dados switch
    {
        { Ativo: false } => "TextoApagado",
        { PrimeiroAcesso: true } => "Aviso",
        _ => "Sucesso"
    };

    public string Papel => Dados.Administrador ? "administrador" : string.Empty;
    public bool EhAdministrador => Dados.Administrador;

    public string AcaoAtivo => Dados.Ativo ? "Desativar" : "Reativar";
}

/// <summary>Configurações do sistema. Hoje, só os acessos.</summary>
/// <remarks>
/// A aba Usuários aparece apenas para o administrador (decidido em 23/10). Esconder no
/// menu não é tranca: cada comando confere de novo antes de tocar no banco, porque menu
/// escondido só esconde — não impede.
///
/// Os dados do escritório (RF-51) entram aqui como segunda aba quando chegar a vez.
/// </remarks>
public partial class ConfiguracoesViewModel : ViewModelBase
{
    private readonly UsuarioDAO _usuarios = new();
    private readonly EscritorioDAO _escritorios = new();

    public ConfiguracoesViewModel()
    {
        if (PodeAdministrar)
        {
            _ = Carregar();
            _ = CarregarEscritorio();
            _ = CarregarBackup();
        }
    }

    /// <summary>Quem não administra não vê a aba nem alcança os comandos.</summary>
    public bool PodeAdministrar => Sessao.EhAdministrador;

    public ObservableCollection<LinhaUsuarioViewModel> Usuarios { get; } = [];

    [ObservableProperty] public partial bool Carregando { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? Aviso { get; set; }

    /// <summary>O formulário de acesso novo, aberto por cima da lista.</summary>
    [ObservableProperty] public partial NovoUsuarioViewModel? Formulario { get; set; }

    /// <summary>A redefinição de senha, aberta por cima da lista.</summary>
    [ObservableProperty] public partial ResetarSenhaViewModel? Redefinicao { get; set; }

    /// <summary>A exportação dos documentos, aberta por cima da lista.</summary>
    [ObservableProperty] public partial ExportarDocumentosViewModel? Exportacao { get; set; }

    /// <summary>O formulário dos dados do escritório, aberto por cima da lista.</summary>
    [ObservableProperty] public partial EscritorioViewModel? Escritorio { get; set; }

    /// <summary>Os dados que já estão gravados, para o resumo do cartão.</summary>
    [ObservableProperty] public partial Escritorio? DadosDoEscritorio { get; set; }

    public string ResumoDoEscritorio => DadosDoEscritorio is { } e
        ? string.Join(" · ", new[] { e.Nome, e.Cidade, e.Uf }.Where(p => !string.IsNullOrWhiteSpace(p)))
        : "Ainda não preenchido. A procuração sai sem a outorgada enquanto estiver assim.";

    /// <summary>Como está a cópia de segurança. O painel só avisa quando atrasa; aqui
    /// o estado aparece também quando está em dia, que é o que permite confiar no aviso.</summary>
    [ObservableProperty] public partial EstadoDoBackupViewModel? Backup { get; set; }

    private async Task CarregarBackup() => Backup = await EstadoDoBackupViewModel.LerAsync();

    /// <summary>O que mudou em cada versão, e qual está rodando.</summary>
    public IReadOnlyList<NotaDaVersao> Versoes => NotasDaVersao.Todas;
    public string VersaoAtual => $"Versão {NotasDaVersao.VersaoAtual}";

    /// <summary>Pergunta aberta por cima da tela. Nula quando não há nenhuma.</summary>
    [ObservableProperty] public partial ConfirmacaoViewModel? Confirmacao { get; set; }

    public bool SemUsuarios => !Carregando && Usuarios.Count == 0;

    private async Task Carregar()
    {
        Carregando = true;

        try
        {
            var lista = await _usuarios.ListarAsync();

            Usuarios.Clear();
            foreach (var usuario in lista)
            {
                Usuarios.Add(new LinhaUsuarioViewModel(usuario));
            }
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível carregar os acessos: {excecao.Message}";
        }
        finally
        {
            Carregando = false;
            OnPropertyChanged(nameof(SemUsuarios));
        }
    }

    [RelayCommand]
    private void NovoUsuario()
    {
        if (!PodeAdministrar) return;

        Aviso = null;
        Formulario = new NovoUsuarioViewModel(
            aoFechar: () => Formulario = null,
            aoCriar: async nome =>
            {
                Formulario = null;
                Aviso = $"Acesso criado para {nome}. "
                        + "Informe a senha provisória ao titular do acesso.";
                await Carregar();
            });
    }

    /// <summary>Liga ou desliga o acesso, com confirmação.</summary>
    [RelayCommand]
    private void MudarAtivo(LinhaUsuarioViewModel? linha)
    {
        if (!PodeAdministrar || linha is null) return;

        // Desativar a si mesmo tranca a porta por dentro.
        if (linha.Id == Sessao.Usuario?.Id)
        {
            Confirmacao = new ConfirmacaoViewModel(
                "Este acesso é o seu",
                "Desativar o próprio acesso deixaria o sistema sem administrador. "
                + "Use outro acesso de administrador para isso.",
                botao: null,
                aoConfirmar: null,
                aoCancelar: () => Confirmacao = null);
            return;
        }

        var ligando = !linha.Ativo;
        Confirmacao = new ConfirmacaoViewModel(
            ligando ? "Reativar este acesso?" : "Desativar este acesso?",
            ligando
                ? $"{linha.Nome} volta a poder entrar no sistema."
                : $"{linha.Nome} deixa de entrar no sistema. Os registros feitos por este acesso permanecem.",
            botao: linha.AcaoAtivo,
            aoConfirmar: async () =>
            {
                Confirmacao = null;
                await Tentar(async () =>
                {
                    await _usuarios.MudarAtivoAsync(linha.Id, ligando);
                    await Carregar();
                });
            },
            aoCancelar: () => Confirmacao = null);
    }

    /// <summary>Devolve o acesso a uma senha provisória, informada aqui.</summary>
    [RelayCommand]
    private void Resetar(LinhaUsuarioViewModel? linha)
    {
        if (!PodeAdministrar || linha is null) return;

        Aviso = null;
        Redefinicao = new ResetarSenhaViewModel(
            linha.Dados,
            aoFechar: () => Redefinicao = null,
            aoResetar: async () =>
            {
                Redefinicao = null;
                Aviso = $"Senha redefinida para {linha.Nome}. "
                        + "Informe a senha provisória ao titular do acesso.";
                await Carregar();
            });
    }

    /// <summary>Abre o formulário dos dados da despachante que assina os documentos.</summary>
    [RelayCommand]
    private void EditarEscritorio()
    {
        if (!PodeAdministrar) return;

        Aviso = null;
        Escritorio = new EscritorioViewModel(
            DadosDoEscritorio,
            aoFechar: () => Escritorio = null,
            aoSalvar: async () =>
            {
                Escritorio = null;
                await CarregarEscritorio();
            });
    }

    private async Task CarregarEscritorio()
    {
        try
        {
            DadosDoEscritorio = await _escritorios.BuscarAsync();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível ler os dados do escritório: {excecao.Message}";
        }
        finally
        {
            OnPropertyChanged(nameof(ResumoDoEscritorio));
        }
    }

    /// <summary>Abre a exportação em massa dos documentos anexados.</summary>
    [RelayCommand]
    private void ExportarDocumentos()
    {
        if (!PodeAdministrar) return;

        Aviso = null;
        Exportacao = new ExportarDocumentosViewModel(aoFechar: () => Exportacao = null);
    }

    private async Task Tentar(Func<Task> acao)
    {
        Erro = null;

        try
        {
            await acao();
        }
        catch (Exception excecao)
        {
            Erro = excecao.Message;
        }
    }

    partial void OnCarregandoChanged(bool valor) => OnPropertyChanged(nameof(SemUsuarios));
}
