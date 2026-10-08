namespace GestorRecursos.Core.Models;

// Camada Core / Models: entidades que representam as tabelas do banco.
public class Cliente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string Whatsapp { get; set; } = string.Empty;

    // CNH: um por cliente e sem histórico, por isso mora aqui e não em tabela própria.
    public string? CnhNumero { get; set; }
    public string? CnhCategoria { get; set; }
    public DateTime? CnhValidade { get; set; }
    public string? CnhRegistro { get; set; }

    // Os cinco abaixo existem para a procuração, não para o processo.
    // Ela disse não precisar deles no cadastro, mas os documentos usam todos.
    public string? Rg { get; set; }
    public string? RgOrgao { get; set; }
    public string? Nacionalidade { get; set; }
    public string? EstadoCivil { get; set; }
    public string? Profissao { get; set; }

    public string? Observacoes { get; set; }

    /// <summary>Cliente arquivado some das listas, mas o histórico continua.</summary>
    public bool Arquivado { get; set; }

    public DateTime DataCadastro { get; set; }

    public Endereco? Endereco { get; set; }

    /// <summary>Veículos ligados a este cliente, com o papel de cada um.</summary>
    public List<VeiculoDoCliente> Veiculos { get; set; } = [];
}
