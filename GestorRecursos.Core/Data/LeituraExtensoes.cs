using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: atalhos de leitura e de parâmetros.
//
// Existe para o DAO não repetir "IsDBNull" a cada coluna opcional. Sem isto,
// cada mapeamento vira três linhas de verificação e o SQL some no meio do ruído.
internal static class LeituraExtensoes
{
    public static string Texto(this OracleDataReader reader, int coluna) =>
        reader.IsDBNull(coluna) ? string.Empty : reader.GetString(coluna);

    public static string? TextoOuNulo(this OracleDataReader reader, int coluna) =>
        reader.IsDBNull(coluna) ? null : reader.GetString(coluna);

    public static int Inteiro(this OracleDataReader reader, int coluna) =>
        reader.IsDBNull(coluna) ? 0 : Convert.ToInt32(reader.GetValue(coluna));

    public static int? InteiroOuNulo(this OracleDataReader reader, int coluna) =>
        reader.IsDBNull(coluna) ? null : Convert.ToInt32(reader.GetValue(coluna));

    public static decimal Decimal(this OracleDataReader reader, int coluna) =>
        reader.IsDBNull(coluna) ? 0m : reader.GetDecimal(coluna);

    // Valor ausente é diferente de zero: uma multa sem valor informado não é uma multa de R$ 0.
    public static decimal? DecimalOuNulo(this OracleDataReader reader, int coluna) =>
        reader.IsDBNull(coluna) ? null : reader.GetDecimal(coluna);

    public static DateTime Data(this OracleDataReader reader, int coluna) =>
        reader.IsDBNull(coluna) ? default : reader.GetDateTime(coluna);

    public static DateTime? DataOuNulo(this OracleDataReader reader, int coluna) =>
        reader.IsDBNull(coluna) ? null : reader.GetDateTime(coluna);

    // O banco guarda 'S' ou 'N': é o formato que o Oracle usa para booleano.
    public static bool Booleano(this OracleDataReader reader, int coluna) =>
        !reader.IsDBNull(coluna) && reader.GetString(coluna) == "S";

    public static string ParaSN(this bool valor) => valor ? "S" : "N";
}

internal static class ComandoExtensoes
{
    /// <summary>Adiciona um parâmetro nomeado, convertendo texto vazio em NULL.</summary>
    /// <remarks>
    /// Texto vazio e NULL são coisas diferentes para o banco: o UNIQUE de e-mail
    /// aceita vários NULL, mas recusa o segundo "". Normalizar aqui evita a armadilha.
    /// </remarks>
    public static OracleCommand Param(this OracleCommand cmd, string nome, object? valor)
    {
        if (valor is string texto && string.IsNullOrWhiteSpace(texto))
        {
            valor = null;
        }

        cmd.Parameters.Add(new OracleParameter(nome, valor ?? DBNull.Value));
        return cmd;
    }

    /// <summary>Cria o comando já ligado à conexão e, quando houver, à transação.</summary>
    public static OracleCommand Comando(this OracleConnection conexao, string sql, OracleTransaction? transacao = null)
    {
        var cmd = new OracleCommand(sql, conexao) { BindByName = true };
        if (transacao is not null)
        {
            cmd.Transaction = transacao;
        }

        return cmd;
    }
}
