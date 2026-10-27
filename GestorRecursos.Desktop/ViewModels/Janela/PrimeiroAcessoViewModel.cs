using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;
using GestorRecursos.Core.Seguranca;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>A troca obrigatória da senha inicial, logo depois de entrar.</summary>
/// <remarks>
/// O acesso nasce no banco, com uma senha que o administrador escolheu e portanto
/// conhece. Enquanto ela não for trocada, a conta não é de quem a usa — por isso esta
/// janela não tem como ser fechada nem adiada: não há "depois", não há X e o Esc não faz
/// nada. O sistema fica visível atrás de propósito, para ficar claro que ela já entrou.
///
/// O e-mail vem junto, e não numa segunda etapa, porque é ele que permite recuperar a
/// senha sozinha. Pedir depois seria pedir nunca.
/// </remarks>
public partial class PrimeiroAcessoViewModel : ViewModelBase
{
    private readonly UsuarioDAO _usuarios = new();
    private readonly Usuario _usuario;
    private readonly Action _aoConcluir;

    public PrimeiroAcessoViewModel(Usuario usuario, Action aoConcluir)
    {
        _usuario = usuario;
        _aoConcluir = aoConcluir;
    }

    public string Titulo => "Primeiro acesso";

    [ObservableProperty] public partial string Senha { get; set; } = string.Empty;
    [ObservableProperty] public partial string SenhaConfirma { get; set; } = string.Empty;
    [ObservableProperty] public partial string Email { get; set; } = string.Empty;

    [ObservableProperty] public partial bool SenhaVisivel { get; set; }
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial bool Ocupado { get; set; }

    [RelayCommand]
    private void AlternarSenha() => SenhaVisivel = !SenhaVisivel;

    [RelayCommand]
    private async Task Concluir()
    {
        if (Senha.Length < 6)
        {
            Erro = "A senha precisa de pelo menos 6 caracteres.";
            return;
        }

        if (Senha != SenhaConfirma)
        {
            Erro = "As duas senhas não são iguais.";
            return;
        }

        if (!EmailValido(Email))
        {
            Erro = "Informe um e-mail válido: é para lá que vai o código quando você esquecer a senha.";
            return;
        }

        Ocupado = true;
        Erro = null;

        try
        {
            await _usuarios.TrocarSenhaAsync(_usuario.Id, Senhas.Embaralhar(Senha));
            await _usuarios.DefinirEmailAsync(_usuario.Id, Email);

            // A sessão em memória também muda: sem isto a janela voltaria a aparecer na
            // próxima vez que alguém perguntasse se o primeiro acesso já foi feito.
            _usuario.EmailRecuperacao = Email.Trim();

            _aoConcluir();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível salvar: {excecao.Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }

    // Validação curta de propósito: o e-mail certo se prova ao receber o código, e uma
    // regra longa só reprovaria endereço válido de formato incomum.
    private static bool EmailValido(string? email)
    {
        var texto = email?.Trim() ?? string.Empty;
        var arroba = texto.IndexOf('@');
        return arroba > 0 && texto.LastIndexOf('.') > arroba + 1 && !texto.EndsWith('.');
    }
}
