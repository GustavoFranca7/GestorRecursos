namespace GestorRecursos.Core.Models;

// Quem entra no sistema: ela e o pai, ambos veem tudo (RNF-01).
public class Usuario
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;

    /// <summary>Hash da senha. Senha em texto puro nunca passa por aqui.</summary>
    public string SenhaHash { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    /// <summary>Administra usuários. Refina o RNF-01: ver e lançar continua igual para todos.</summary>
    public bool Administrador { get; set; }

    /// <summary>Para onde vai o código de recuperação.</summary>
    public string? EmailRecuperacao { get; set; }

    /// <summary>Sem e-mail, o primeiro acesso ainda não foi concluído.</summary>
    public bool PrimeiroAcesso => string.IsNullOrWhiteSpace(EmailRecuperacao);

    public string? CodigoHash { get; set; }
    public DateTime? CodigoExpiraEm { get; set; }
    public int CodigoTentativas { get; set; }

    /// <summary>A inicial que aparece no círculo da barra de cima.</summary>
    public string Inicial =>
        string.IsNullOrWhiteSpace(Nome) ? "?" : Nome.Trim()[..1].ToUpperInvariant();

    /// <summary>Só o primeiro nome: a barra é estreita e o resto não cabe.</summary>
    public string PrimeiroNome =>
        string.IsNullOrWhiteSpace(Nome) ? string.Empty : Nome.Trim().Split(' ')[0];
}
