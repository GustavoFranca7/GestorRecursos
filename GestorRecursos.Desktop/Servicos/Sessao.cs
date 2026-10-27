using GestorRecursos.Core.Models;

namespace GestorRecursos.Desktop.Servicos;

/// <summary>Quem está usando o sistema agora.</summary>
/// <remarks>
/// Estático pelo mesmo motivo da <see cref="AreaDeTransferencia"/>: é um dado só, da
/// sessão inteira, e passá-lo de ViewModel em ViewModel até a barra de cima daria
/// acoplamento sem nenhuma clareza em troca.
///
/// Vale enquanto o programa estiver aberto. Não é lembrança entre execuções: fechar e
/// abrir pede a senha de novo, que é o que se espera de um sistema com dado de cliente.
/// </remarks>
public static class Sessao
{
    public static Usuario? Usuario { get; private set; }

    public static void Entrar(Usuario usuario) => Usuario = usuario;

    public static void Sair() => Usuario = null;

    /// <summary>Só o administrador vê a aba Usuários e cria acesso.</summary>
    public static bool EhAdministrador => Usuario is { Administrador: true };

    public static string Nome => Usuario?.PrimeiroNome ?? string.Empty;
    public static string Inicial => Usuario?.Inicial ?? "?";
}
