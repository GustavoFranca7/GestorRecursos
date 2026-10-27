using GestorRecursos.Core.Models;

namespace GestorRecursos.Core.Importacao;

/// <summary>Em que pé ficou a leitura de um documento.</summary>
public enum SituacaoLeitura
{
    /// <summary>Tudo que o cadastro precisa veio, e as conferências passaram.</summary>
    Pronto,

    /// <summary>Dá para importar, mas falta dado ou alguma conferência não fechou.</summary>
    Conferir,

    /// <summary>Não entra no banco: ou não deu para ler, ou a leitura não é confiável.</summary>
    Recusado
}

/// <summary>O que saiu de uma procuração.</summary>
/// <remarks>
/// O <see cref="Cliente"/> nunca vai direto para o banco: a tela mostra esta leitura, e
/// quem confere decide. Documento é papel digitado à mão — o que foi extraído é proposta,
/// não verdade.
/// </remarks>
public sealed class ProcuracaoLida
{
    public required string Arquivo { get; init; }

    /// <summary>Nulo quando a situação é <see cref="SituacaoLeitura.Recusado"/>.</summary>
    public Cliente? Cliente { get; init; }

    /// <summary>A linha de endereço como estava escrita, para conferência.</summary>
    public string? EnderecoNoDocumento { get; init; }

    public SituacaoLeitura Situacao { get; init; }

    /// <summary>O que quem confere precisa saber. Vazio quando nada chamou atenção.</summary>
    public IReadOnlyList<string> Avisos { get; init; } = [];

    /// <summary>Preenchido depois, quando o CPF já existe no banco.</summary>
    public string? JaCadastrado { get; set; }

    public static ProcuracaoLida Recusar(string arquivo, string motivo) => new()
    {
        Arquivo = arquivo,
        Situacao = SituacaoLeitura.Recusado,
        Avisos = [motivo]
    };
}
