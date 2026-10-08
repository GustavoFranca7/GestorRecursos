namespace GestorRecursos.Core.Models;

/// <summary>Uma linha da lista de clientes.</summary>
/// <remarks>
/// Não é o <see cref="Cliente"/> cortado: é o que a lista mostra, incluindo coisas que
/// não moram na tabela de clientes (placas, valor em aberto, prazo mais próximo). Trazer
/// isso numa consulta só evita uma ida ao banco por linha da tela.
/// </remarks>
public class ResumoCliente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Whatsapp { get; set; } = string.Empty;
    public string? Cidade { get; set; }
    public string? Estado { get; set; }

    /// <summary>Placas dos veículos ligados ao cliente, separadas por vírgula.</summary>
    public string? Placas { get; set; }

    /// <summary>Soma do que falta receber em todas as contratações dele.</summary>
    public decimal EmAberto { get; set; }

    /// <summary>Prazo de protocolo mais próximo entre os recursos ainda não protocolados.</summary>
    public DateTime? PrazoMaisProximo { get; set; }

    /// <summary>Fase do recurso a que esse prazo pertence: autuação, JARI ou CETRAN.</summary>
    public string? FaseDoPrazo { get; set; }

    public bool Arquivado { get; set; }
}

/// <summary>Os três números do cabeçalho da lista.</summary>
public class ContagemClientes
{
    public int Cadastrados { get; set; }
    public int ComValorEmAberto { get; set; }
    public int ComPrazoNaSemana { get; set; }
}

/// <summary>O que sai junto quando um cliente é apagado de vez.</summary>
public class ResumoExclusao
{
    /// <summary>Só o cliente arquivado pode ser apagado — a tela confere antes de oferecer.</summary>
    public bool Arquivado { get; set; }

    public int Contratacoes { get; set; }
    public int Processos { get; set; }
    public int Pagamentos { get; set; }
    public decimal TotalPago { get; set; }

    /// <summary>Tem histórico financeiro: a confirmação precisa dizer isso em voz alta.</summary>
    public bool LevaHistorico => Contratacoes > 0 || Pagamentos > 0 || Processos > 0;
}

/// <summary>As abas de filtro da lista de clientes.</summary>
public enum FiltroClientes
{
    Ativos,
    ComPendencia,
    ComPrazo,
    Arquivados
}
