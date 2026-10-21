namespace GestorRecursos.Core.Documentos;

/// <summary>O papel que cada trecho faz no documento, e não como ele é pintado.</summary>
/// <remarks>
/// A Core não conhece fonte nem margem: quem exibe decide. Guardar "título" em vez de
/// "negrito 16" permite que a mesma montagem sirva à tela e ao arquivo gerado.
/// </remarks>
public enum EstiloTrecho
{
    Titulo,
    Paragrafo,
    Centralizado,

    /// <summary>A linha sobre a qual se assina, seguida do nome de quem assina.</summary>
    Assinatura
}

public sealed record TrechoDocumento(string Texto, EstiloTrecho Estilo);

/// <summary>Um documento montado, pronto para ser exibido ou gravado.</summary>
public sealed class Documento
{
    public required string Titulo { get; init; }

    /// <summary>Nome sugerido para o arquivo, sem extensão.</summary>
    public required string NomeArquivo { get; init; }

    public required IReadOnlyList<TrechoDocumento> Trechos { get; init; }

    public string TextoSimples() =>
        string.Join(Environment.NewLine + Environment.NewLine, Trechos.Select(t => t.Texto));
}
