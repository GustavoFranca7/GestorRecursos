using System.Text;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: tudo que conversa diretamente com o Oracle.
public static class Database
{
    // Configuração lida de fora do código, para a senha não ir para o Git.
    // Cada fonte sobrescreve a anterior: arquivo -> cofre local -> ambiente.
    private static readonly IConfiguration Configuracao = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddUserSecrets(typeof(Database).Assembly, optional: true)
        .AddEnvironmentVariables()
        .Build();

    // Montada uma vez: a configuração da wallet é estática no driver e não pode
    // ser aplicada a cada conexão.
    private static readonly string StringConexao = PrepararConexao();

    /// <summary>Abre uma conexão já pronta para uso.</summary>
    /// <remarks>
    /// Assíncrono porque o banco está na nuvem: cada ida à rede custa dezenas de
    /// milissegundos, e segurar a thread nesse tempo trava a interface.
    /// Quem chama é dono da conexão e deve descartá-la (<c>await using</c>).
    /// </remarks>
    public static async Task<OracleConnection> AbrirAsync(CancellationToken cancelamento = default)
    {
        var conexao = new OracleConnection(StringConexao);
        try
        {
            await conexao.OpenAsync(cancelamento);
            return conexao;
        }
        catch
        {
            // Sem isto, uma falha na abertura deixaria a conexão pendurada.
            await conexao.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Separa o que é conexão do que é configuração da wallet e devolve a string limpa.
    /// </summary>
    /// <remarks>
    /// O driver gerenciado (Oracle.ManagedDataAccess.Core) recusa <c>TnsAdmin</c> e
    /// <c>WalletLocation</c> dentro da connection string — eles existiam no driver
    /// antigo, e a documentação da Oracle ainda mistura os dois formatos. Aqui os
    /// atributos são aceitos, retirados da string e aplicados em
    /// <see cref="OracleConfiguration"/>, que é onde o driver gerenciado os lê.
    /// Também funciona configurar por <c>Oracle:TnsAdmin</c>, sem sujar a string.
    /// </remarks>
    private static string PrepararConexao()
    {
        var bruta = Configuracao.GetConnectionString("Oracle")
            ?? throw new InvalidOperationException(
                "A connection string 'Oracle' não foi configurada.\n" +
                "Na pasta do projeto GestorRecursos.Core, execute:\n" +
                "  dotnet user-secrets set \"ConnectionStrings:Oracle\" \"User Id=...;Password=...;Data Source=...\"");

        var limpa = new StringBuilder();
        string? tnsAdmin = Configuracao["Oracle:TnsAdmin"];
        string? walletLocation = Configuracao["Oracle:WalletLocation"];

        foreach (var parte in bruta.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separador = parte.IndexOf('=');
            if (separador <= 0)
            {
                continue;
            }

            var chave = parte[..separador].Trim().Replace(" ", string.Empty);
            var valor = parte[(separador + 1)..].Trim();

            if (chave.Equals("TnsAdmin", StringComparison.OrdinalIgnoreCase))
            {
                tnsAdmin ??= valor;
            }
            else if (chave.Equals("WalletLocation", StringComparison.OrdinalIgnoreCase))
            {
                walletLocation ??= valor;
            }
            else
            {
                limpa.Append(parte).Append(';');
            }
        }

        // A wallet fica na mesma pasta do tnsnames.ora, salvo configuração explícita.
        walletLocation ??= tnsAdmin;

        if (!string.IsNullOrWhiteSpace(tnsAdmin))
        {
            if (!Directory.Exists(tnsAdmin))
            {
                throw new DirectoryNotFoundException(
                    $"A pasta da wallet não existe: {tnsAdmin}\n" +
                    "Confira o caminho configurado em TnsAdmin.");
            }

            OracleConfiguration.TnsAdmin = tnsAdmin;
            OracleConfiguration.WalletLocation = walletLocation;
        }

        return limpa.ToString();
    }
}
