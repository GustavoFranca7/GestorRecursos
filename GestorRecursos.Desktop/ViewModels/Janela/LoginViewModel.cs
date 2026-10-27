using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Integracoes;
using GestorRecursos.Core.Models;
using GestorRecursos.Core.Seguranca;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Em que ponto da entrada a pessoa está.</summary>
public enum PassoLogin
{
    Entrar,

    /// <summary>Esqueceu a senha: informa o login para receber o código.</summary>
    PedirCodigo,

    /// <summary>Recebeu o código: digita e escolhe a senha nova.</summary>
    TrocarSenha
}

/// <summary>A porta do sistema.</summary>
/// <remarks>
/// Antes o programa abria direto na lista de clientes. O login separa os dois acessos
/// (RNF-01) e é o que o RNF-02 pede — sem complicar, que foi o pedido dela.
///
/// <b>A tela não cria usuário.</b> Acesso novo nasce no banco, pela mão de quem
/// administra; uma tela que cadastra sozinha deixaria qualquer um entrar. A troca da
/// senha inicial e o e-mail de recuperação são cobrados depois de entrar, pela janela de
/// primeiro acesso.
///
/// A recuperação manda um código de seis dígitos por e-mail, digitado aqui mesmo. Link
/// exigiria uma página na web para abrir; um programa de mesa tem onde digitar.
/// </remarks>
public partial class LoginViewModel : ViewModelBase
{
    /// <summary>Quanto tempo o código vale. Curto: ela pede e usa na hora.</summary>
    private const int MinutosDoCodigo = 15;

    /// <summary>Erros seguidos antes de queimar o código.</summary>
    private const int TentativasDoCodigo = 3;

    private readonly UsuarioDAO _usuarios = new();
    private readonly EnvioDeEmail _email = new();
    private readonly Action<Usuario> _aoEntrar;

    /// <summary>Quem pediu o código. Guardado entre os dois passos da troca.</summary>
    private Usuario? _recuperando;

    public LoginViewModel(Action<Usuario> aoEntrar) => _aoEntrar = aoEntrar;

    [ObservableProperty] public partial PassoLogin Passo { get; set; } = PassoLogin.Entrar;

    [ObservableProperty] public partial string Login { get; set; } = string.Empty;
    [ObservableProperty] public partial string Senha { get; set; } = string.Empty;
    [ObservableProperty] public partial string Codigo { get; set; } = string.Empty;
    [ObservableProperty] public partial string SenhaNova { get; set; } = string.Empty;
    [ObservableProperty] public partial string SenhaConfirma { get; set; } = string.Empty;

    /// <summary>O olho: quando ligado, a senha aparece em texto.</summary>
    [ObservableProperty] public partial bool SenhaVisivel { get; set; }

    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial string? Aviso { get; set; }
    [ObservableProperty] public partial bool Ocupado { get; set; }

    public bool NoEntrar => Passo == PassoLogin.Entrar;
    public bool NoPedirCodigo => Passo == PassoLogin.PedirCodigo;
    public bool NoTrocarSenha => Passo == PassoLogin.TrocarSenha;

    /// <summary>Voltar ao login só faz sentido quando não se está nele.</summary>
    public bool PodeVoltar => Passo != PassoLogin.Entrar;

    public string Titulo => Passo switch
    {
        PassoLogin.PedirCodigo => "Esqueci a senha",
        PassoLogin.TrocarSenha => "Escolha a senha nova",
        _ => "Acesso ao sistema"
    };

    public string Subtitulo => Passo switch
    {
        PassoLogin.PedirCodigo => "Digite seu usuário. Enviaremos um código para o e-mail cadastrado.",
        PassoLogin.TrocarSenha => "O código chegou por e-mail e vale por alguns minutos.",
        _ => "Entre com seu usuário e senha."
    };

    [RelayCommand]
    private async Task Entrar()
    {
        if (Vazio(Login, "Digite seu usuário.") || Vazio(Senha, "Digite sua senha.")) return;

        await Tentar(async () =>
        {
            var usuario = await _usuarios.BuscarPorLoginAsync(Login);

            // Mesma resposta para login inexistente e senha errada: dizer qual dos dois
            // falhou entrega metade da resposta a quem está tentando adivinhar.
            if (usuario is null || !Senhas.Confere(Senha, usuario.SenhaHash))
            {
                Erro = "Usuário ou senha incorretos.";
                return;
            }

            if (!usuario.Ativo)
            {
                Erro = "Este acesso está desativado.";
                return;
            }

            // Primeiro acesso entra: a troca da senha é cobrada lá dentro, por uma
            // janela que não dá para fechar.
            Sessao.Entrar(usuario);
            _aoEntrar(usuario);
        });
    }

    [RelayCommand]
    private void EsqueciSenha()
    {
        Codigo = string.Empty;
        SenhaNova = string.Empty;
        SenhaConfirma = string.Empty;
        Ir(PassoLogin.PedirCodigo);
    }

    [RelayCommand]
    private async Task EnviarCodigo()
    {
        if (Vazio(Login, "Digite seu usuário.")) return;

        await Tentar(async () =>
        {
            var usuario = await _usuarios.BuscarPorLoginAsync(Login);

            // Sistema de duas pessoas: esconder que o usuário não existe só geraria
            // confusão, sem esconder nada de quem já está na máquina dela.
            if (usuario is null)
            {
                Erro = "Não existe usuário com esse nome.";
                return;
            }

            if (string.IsNullOrWhiteSpace(usuario.EmailRecuperacao))
            {
                Erro = "Este acesso ainda não tem e-mail de recuperação. Entre com a senha que recebeu e cadastre o e-mail.";
                return;
            }

            var codigo = Senhas.SortearCodigo();
            var expira = DateTime.Now.AddMinutes(MinutosDoCodigo);

            await _usuarios.GravarCodigoAsync(usuario.Id, Senhas.Embaralhar(codigo), expira);

            var envio = await _email.EnviarCodigoAsync(usuario.EmailRecuperacao, codigo, MinutosDoCodigo);
            if (envio.Situacao != SituacaoEnvio.Enviado)
            {
                Erro = envio.Situacao == SituacaoEnvio.SemConfiguracao
                    ? "O envio de e-mail não está configurado neste computador. Fale com quem instalou o sistema."
                    : $"Não foi possível enviar o e-mail: {envio.Detalhe}";
                return;
            }

            _recuperando = usuario;
            Aviso = $"Código enviado para {Esconder(usuario.EmailRecuperacao)}. Ele vale por {MinutosDoCodigo} minutos.";
            Ir(PassoLogin.TrocarSenha, limparAviso: false);
        });
    }

    [RelayCommand]
    private async Task ConfirmarTroca()
    {
        if (_recuperando is not { } usuario) return;

        if (Vazio(Codigo, "Digite o código que chegou por e-mail.")) return;

        if (SenhaNova.Length < 6)
        {
            Erro = "A senha precisa de pelo menos 6 caracteres.";
            return;
        }

        if (SenhaNova != SenhaConfirma)
        {
            Erro = "As duas senhas não são iguais.";
            return;
        }

        await Tentar(async () =>
        {
            var atual = await _usuarios.BuscarPorLoginAsync(usuario.Login);

            if (atual?.CodigoHash is null || atual.CodigoExpiraEm is null)
            {
                Erro = "O código não vale mais. Peça outro.";
                Ir(PassoLogin.PedirCodigo);
                return;
            }

            if (atual.CodigoExpiraEm < DateTime.Now)
            {
                await _usuarios.InvalidarCodigoAsync(atual.Id);
                Erro = "O código venceu. Peça outro.";
                Ir(PassoLogin.PedirCodigo);
                return;
            }

            if (!Senhas.Confere(Codigo.Trim(), atual.CodigoHash))
            {
                var erros = await _usuarios.ErrouCodigoAsync(atual.Id);
                if (erros >= TentativasDoCodigo)
                {
                    await _usuarios.InvalidarCodigoAsync(atual.Id);
                    Erro = "Código errado três vezes. Peça um código novo.";
                    Ir(PassoLogin.PedirCodigo);
                    return;
                }

                Erro = $"Código incorreto. Restam {TentativasDoCodigo - erros} tentativas.";
                return;
            }

            await _usuarios.TrocarSenhaAsync(atual.Id, Senhas.Embaralhar(SenhaNova));

            Senha = string.Empty;
            Ir(PassoLogin.Entrar, limparAviso: false);
            Aviso = "Senha trocada. Entre com a senha nova.";
        });
    }

    [RelayCommand]
    private void Voltar()
    {
        _recuperando = null;
        Senha = string.Empty;
        Ir(PassoLogin.Entrar);
    }

    [RelayCommand]
    private void AlternarSenha() => SenhaVisivel = !SenhaVisivel;

    private void Ir(PassoLogin passo, bool limparAviso = true)
    {
        Erro = null;
        if (limparAviso) Aviso = null;
        Passo = passo;
    }

    private bool Vazio(string valor, string recado)
    {
        if (!string.IsNullOrWhiteSpace(valor)) return false;

        Erro = recado;
        return true;
    }

    // "usuario@gmail.com" vira "usu***@gmail.com": confirma o endereço certo sem
    // escrevê-lo inteiro na tela de quem talvez não seja a dona da conta.
    private static string Esconder(string email)
    {
        var arroba = email.IndexOf('@');
        if (arroba <= 3) return email;

        return $"{email[..3]}***{email[arroba..]}";
    }

    private async Task Tentar(Func<Task> acao)
    {
        Ocupado = true;
        Erro = null;

        try
        {
            await acao();
        }
        catch (Exception excecao)
        {
            Erro = excecao.Message;
        }
        finally
        {
            Ocupado = false;
        }
    }

    partial void OnPassoChanged(PassoLogin valor)
    {
        OnPropertyChanged(nameof(NoEntrar));
        OnPropertyChanged(nameof(NoPedirCodigo));
        OnPropertyChanged(nameof(NoTrocarSenha));
        OnPropertyChanged(nameof(PodeVoltar));
        OnPropertyChanged(nameof(Titulo));
        OnPropertyChanged(nameof(Subtitulo));
    }
}
