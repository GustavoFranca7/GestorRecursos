using System.Security.Cryptography;

namespace GestorRecursos.Core.Seguranca;

/// <summary>Guarda e confere senha sem nunca saber qual ela é.</summary>
/// <remarks>
/// PBKDF2 com SHA-256, do próprio .NET: não entra biblioteca nova e é o que a Microsoft
/// usa no Identity. O sal é sorteado por senha, então duas pessoas com a mesma senha
/// geram hashes diferentes, e as iterações tornam a tentativa em massa cara.
///
/// Serve também ao código de recuperação, pelo mesmo motivo: quem conseguir ler a tabela
/// não pode entrar com o que leu.
/// </remarks>
public static class Senhas
{
    // Custo de hoje. Guardado junto do hash para poder subir depois sem invalidar o
    // que já está gravado: o conferidor usa as iterações que vieram com o hash.
    private const int Iteracoes = 210_000;
    private const int TamanhoSal = 16;
    private const int TamanhoHash = 32;

    private static readonly HashAlgorithmName Algoritmo = HashAlgorithmName.SHA256;

    /// <summary>Devolve "iterações.sal.hash", pronto para gravar.</summary>
    public static string Embaralhar(string senha)
    {
        var sal = RandomNumberGenerator.GetBytes(TamanhoSal);
        var hash = Rfc2898DeriveBytes.Pbkdf2(senha, sal, Iteracoes, Algoritmo, TamanhoHash);

        return $"{Iteracoes}.{Convert.ToBase64String(sal)}.{Convert.ToBase64String(hash)}";
    }

    /// <summary>Confere a senha digitada contra o que está gravado.</summary>
    public static bool Confere(string? senha, string? guardado)
    {
        if (string.IsNullOrEmpty(senha) || string.IsNullOrWhiteSpace(guardado))
        {
            return false;
        }

        var partes = guardado.Split('.');
        if (partes.Length != 3 || !int.TryParse(partes[0], out var iteracoes))
        {
            return false;
        }

        try
        {
            var sal = Convert.FromBase64String(partes[1]);
            var esperado = Convert.FromBase64String(partes[2]);
            var obtido = Rfc2898DeriveBytes.Pbkdf2(senha, sal, iteracoes, Algoritmo, esperado.Length);

            // Comparação de tempo fixo: comparar byte a byte entrega o tamanho do
            // acerto pelo tempo de resposta.
            return CryptographicOperations.FixedTimeEquals(obtido, esperado);
        }
        catch (FormatException)
        {
            // Hash gravado fora do formato: trata como senha errada, não como falha.
            return false;
        }
    }

    /// <summary>Sorteia o código de 6 dígitos da recuperação.</summary>
    /// <remarks>
    /// Sorteado pelo gerador criptográfico, e não por <c>Random</c>: um código de
    /// recuperação previsível é uma porta aberta.
    /// </remarks>
    public static string SortearCodigo() =>
        RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
}
