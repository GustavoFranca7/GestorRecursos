namespace GestorRecursos.Core.Models;

// A tabela de preços do escritório.
public class TipoServico
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;

    /// <summary>Verdadeiro para o que percorre autuação, JARI e CETRAN.</summary>
    public bool TemInstancias { get; set; }

    public decimal ValorTabela { get; set; }
    public bool Ativo { get; set; } = true;
}
