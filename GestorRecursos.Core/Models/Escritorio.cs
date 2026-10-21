namespace GestorRecursos.Core.Models;

// A outorgada: quem assina a procuração e os recursos. Linha única na tabela
// ESCRITORIO, travada por CHECK (ID = 1) — o escritório é um só.
public class Escritorio
{
    public string Nome { get; set; } = string.Empty;
    public string? Nacionalidade { get; set; }
    public string? EstadoCivil { get; set; }
    public string? Profissao { get; set; }
    public string? Rg { get; set; }
    public string? RgOrgao { get; set; }
    public string? Cpf { get; set; }

    /// <summary>Registro de despachante documentalista. Vazio até sair.</summary>
    public string? RegistroProfissional { get; set; }

    public string? Email { get; set; }

    public string? Logradouro { get; set; }
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string? Cep { get; set; }
    public string? Cidade { get; set; }
    public string? Uf { get; set; }

    /// <summary>Cidade do rodapé dos documentos. A data é sempre a da geração.</summary>
    public string? CidadeAssinatura { get; set; }
}
