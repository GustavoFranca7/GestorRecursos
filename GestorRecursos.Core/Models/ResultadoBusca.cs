namespace GestorRecursos.Core.Models;

/// <summary>O que uma busca global encontrou.</summary>
/// <remarks>
/// Responde à maior dor do escritório: chega uma atualização e ela não sabe de
/// quem é. Por isso a busca aceita qualquer ponta — nome, CPF, placa, AIT ou
/// número de processo — e o resultado sempre aponta para um cliente.
/// </remarks>
public class ResultadoBusca
{
    /// <summary>Onde o termo foi encontrado: CLIENTE, VEICULO, MULTA ou PROCESSO.</summary>
    public string Origem { get; set; } = string.Empty;

    /// <summary>O que casou com a busca: a placa, o AIT, o CPF.</summary>
    public string Encontrado { get; set; } = string.Empty;

    public int ClienteId { get; set; }
    public string ClienteNome { get; set; } = string.Empty;
    public string ClienteCpf { get; set; } = string.Empty;
    public string? ClienteWhatsapp { get; set; }

    /// <summary>Linha de contexto para a tela mostrar sem abrir o cadastro.</summary>
    public string? Detalhe { get; set; }

    public bool ClienteArquivado { get; set; }
}
