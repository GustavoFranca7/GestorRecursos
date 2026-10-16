namespace GestorRecursos.Core.Models;

// Veículo identificado pela placa. Pode não ser do cliente: carro alugado,
// da empresa ou de familiar, com o cliente indicado como condutor.
public class Veiculo
{
    public int Id { get; set; }
    public string Placa { get; set; } = string.Empty;
    public string? Renavam { get; set; }

    // Os três abaixo só aparecem no texto do recurso.
    public string? MarcaModelo { get; set; }
    public string? Cor { get; set; }
    public string? Ano { get; set; }

    public string? Observacao { get; set; }

    /// <summary>Descrição do veículo como o recurso a escreve.</summary>
    public string Descricao()
    {
        var partes = new List<string> { $"placa {Placa}" };

        if (!string.IsNullOrWhiteSpace(MarcaModelo)) partes.Add($"modelo {MarcaModelo}");
        if (!string.IsNullOrWhiteSpace(Ano)) partes.Add(Ano!);
        if (!string.IsNullOrWhiteSpace(Cor)) partes.Add($"cor {Cor}");
        if (!string.IsNullOrWhiteSpace(Renavam)) partes.Add($"RENAVAM {Renavam}");

        return string.Join(", ", partes);
    }
}

/// <summary>Um veículo visto a partir do cliente, com o papel que ele tem nele.</summary>
public class VeiculoDoCliente
{
    public int VinculoId { get; set; }
    public Veiculo Veiculo { get; set; } = new();
    public VinculoVeiculo Vinculo { get; set; }
}

/// <summary>Um veículo do sistema na hora de escolher qual ligar ao cliente.</summary>
/// <remarks>
/// Traz o que ajuda a reconhecer o carro certo na lista: quantos clientes já o têm e
/// quantas multas ele acumula. E diz se este cliente já está ligado a ele, para não
/// oferecer um vínculo que já existe.
/// </remarks>
public class VeiculoParaVincular
{
    public int Id { get; set; }
    public string Placa { get; set; } = string.Empty;
    public string? Renavam { get; set; }
    public string? MarcaModelo { get; set; }
    public string? Cor { get; set; }
    public string? Ano { get; set; }

    public bool JaLigadoAoCliente { get; set; }
    public int Clientes { get; set; }
    public int Multas { get; set; }
}
