namespace GestorRecursos.Core.Models;

// Um endereço por cliente. Existe para a procuração e para o recurso.
public class Endereco
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public string? Cep { get; set; }
    public string Logradouro { get; set; } = string.Empty;
    public string? Numero { get; set; }
    public string? Complemento { get; set; }
    public string? Bairro { get; set; }
    public string Cidade { get; set; } = string.Empty;
    public string Estado { get; set; } = string.Empty;

    /// <summary>Endereço em uma linha, como aparece na procuração.</summary>
    public string Resumo()
    {
        var partes = new List<string> { Logradouro };

        if (!string.IsNullOrWhiteSpace(Numero)) partes.Add($"nº {Numero}");
        if (!string.IsNullOrWhiteSpace(Complemento)) partes.Add(Complemento);
        if (!string.IsNullOrWhiteSpace(Bairro)) partes.Add($"bairro {Bairro}");

        // O banco guarda só os dígitos; num documento assinado, o CEP vai pontuado.
        if (!string.IsNullOrWhiteSpace(Cep))
        {
            var digitos = Validacao.Normalizacao.SomenteDigitos(Cep);
            partes.Add($"CEP {(digitos.Length == 8 ? $"{digitos[..5]}-{digitos[5..]}" : Cep)}");
        }

        partes.Add($"{Cidade} - {Estado}");
        return string.Join(", ", partes);
    }
}
