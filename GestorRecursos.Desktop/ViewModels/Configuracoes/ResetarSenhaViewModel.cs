using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GestorRecursos.Core.Data;
using GestorRecursos.Core.Models;
using GestorRecursos.Core.Seguranca;
using GestorRecursos.Desktop.Servicos;

namespace GestorRecursos.Desktop.ViewModels;

/// <summary>Devolve um acesso a uma senha provisória.</summary>
/// <remarks>
/// A senha é digitada por quem administra, pelo mesmo motivo da criação: é quem vai
/// repassá-la. Gravar também limpa o e-mail de recuperação, o que recoloca o acesso na
/// janela de primeiro acesso — a senha definitiva volta a ser escolhida por quem usa, e
/// não fica conhecida por quem a reativou.
/// </remarks>
public partial class ResetarSenhaViewModel : ViewModelBase
{
    private readonly UsuarioDAO _usuarios = new();
    private readonly Usuario _usuario;
    private readonly Action _aoFechar;
    private readonly Func<Task> _aoResetar;

    public ResetarSenhaViewModel(Usuario usuario, Action aoFechar, Func<Task> aoResetar)
    {
        _usuario = usuario;
        _aoFechar = aoFechar;
        _aoResetar = aoResetar;
    }

    public string Alvo => $"{_usuario.Nome} · @{_usuario.Login}";

    [ObservableProperty] public partial string SenhaProvisoria { get; set; } = string.Empty;
    [ObservableProperty] public partial string? Erro { get; set; }
    [ObservableProperty] public partial bool Ocupado { get; set; }

    [RelayCommand]
    private async Task Confirmar()
    {
        if (!Sessao.EhAdministrador)
        {
            Erro = "Apenas um administrador pode redefinir senha.";
            return;
        }

        var senha = SenhaProvisoria.Trim();
        if (senha.Length < 6)
        {
            Erro = "A senha provisória precisa de pelo menos 6 caracteres.";
            return;
        }

        Ocupado = true;
        Erro = null;

        try
        {
            await _usuarios.ResetarAsync(_usuario.Id, Senhas.Embaralhar(senha));
            await _aoResetar();
        }
        catch (Exception excecao)
        {
            Erro = $"Não foi possível redefinir: {excecao.Message}";
        }
        finally
        {
            Ocupado = false;
        }
    }

    [RelayCommand]
    private void Cancelar() => _aoFechar();
}
