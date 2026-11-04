using System.Globalization;

namespace GestorRecursos.Backup;

/// <summary>Transforma um valor lido do banco no texto que o recria.</summary>
/// <remarks>
/// Aqui concatenação é o objetivo, não um descuido: o produto deste projeto é um
/// arquivo de texto com comandos SQL. A regra do sistema — sempre parametrizar — vale
/// para consulta montada com dado de fora; isto lê do próprio banco e escreve em
/// arquivo, sem nunca executar nada.
/// </remarks>
internal static class ValorSql
{
    /// <summary>O literal SQL de um valor, pronto para entrar num INSERT.</summary>
    public static string De(object? valor) => valor switch
    {
        null or DBNull => "NULL",

        // Data vai com formato explícito: sem isso o arquivo só restauraria numa sessão
        // com o mesmo NLS_DATE_FORMAT de quem exportou, e isso muda de máquina.
        DateTime data =>
            $"TO_DATE('{data:yyyy-MM-dd HH:mm:ss}','YYYY-MM-DD HH24:MI:SS')",

        // Aspas simples dobradas: é como o Oracle escapa aspas dentro de texto. Um nome
        // como O'Brien quebraria o arquivo inteiro sem isto.
        string texto => $"'{texto.Replace("'", "''")}'",

        bool booleano => booleano ? "'S'" : "'N'",

        // Invariante de propósito: no formato brasileiro o separador decimal é vírgula,
        // e 1234,56 viraria dois valores na lista do INSERT.
        decimal numero => numero.ToString(CultureInfo.InvariantCulture),
        double numero => numero.ToString(CultureInfo.InvariantCulture),
        float numero => numero.ToString(CultureInfo.InvariantCulture),

        _ => Convert.ToString(valor, CultureInfo.InvariantCulture) ?? "NULL"
    };
}
