using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;
using GestorRecursos.Core.Seguranca;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Criar um acesso, por cima da lista de usuários.</summary>
/// <remarks>
/// A senha provisória é digitada por quem cria, e não sorteada: quem cria é quem vai
/// repassá-la, e combinar uma senha legível é mais prático do que copiar seis dígitos.
/// Ela dura um acesso só — a definição da senha pessoal é cobrada logo na entrada.
///
/// É também o que torna o <c>--hash</c> desnecessário no dia a dia: o hash sai daqui.
/// </remarks>
public partial class NovoUsuarioViewModel : ViewModelBase
{
    private readonly UsuarioDAO _usuarios = new();
    private readonly Action _aoFechar;
    private readonly Func<string, Task> _aoCriar;

    public NovoUsuarioViewModel(Action aoFechar, Func<string, Task> aoCriar)
    {
        _aoFechar = aoFechar;
        _aoCriar = aoCriar;
    }

    [ObservableProperty] public partial string Nome { get; set; } = string.Empty;
    [ObservableProperty] public partial string Login { get; set; } = string.Empty;
    [ObservableProperty] public partial string SenhaProvisoria { get; set; } = string.Empty;
    [ObservableProperty] public partial bool Administrador { get; set; }

    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial bool Ocupado { get; set; }

    [RelayCommand]
    private async Task Criar()
    {
        if (!Sessao.EhAdministrador)
        {
            Erro = "Só um administrador pode criar acesso.";
            return;
        }

        if (string.IsNullOrWhiteSpace(Nome))
        {
            Erro = "Digite o nome da pessoa.";
            return;
        }

        var login = Login.Trim();
        if (login.Length < 3)
        {
            Erro = "O usuário precisa de pelo menos 3 caracteres.";
            return;
        }

        if (login.Contains(' '))
        {
            Erro = "O usuário não pode ter espaço.";
            return;
        }

        if (SenhaProvisoria.Trim().Length < 6)
        {
            Erro = "A senha provisória precisa de pelo menos 6 caracteres.";
            return;
        }

        Ocupado = true;
        Erro = null;

        try
        {
            if (await _usuarios.BuscarPorLoginAsync(login) is not null)
            {
                Erro = $"Já existe um acesso com o usuário \"{login}\".";
                return;
            }

            // Sem e-mail de propósito: é o vazio que exige a troca da senha ao entrar.
            var provisoria = SenhaProvisoria.Trim();
            var usuario = new Usuario
            {
                Nome = Nome.Trim(),
                Login = login,
                SenhaHash = Senhas.Embaralhar(provisoria),
                Administrador = Administrador
            };

            await _usuarios.InserirAsync(usuario);
            await _aoCriar(usuario.Nome);
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível criar: {excecao.Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand]
    private void Cancelar() => _aoFechar();
}
