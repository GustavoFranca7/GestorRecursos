using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;
using System;

// Camada Core / Data: tudo que conversa diretamente com o Oracle.
namespace GestorRecursos.Core.Data
{
    public static class Database
    {
        // Configuração lida de fora do código, para a senha não ir para o Git.
        // Cada fonte sobrescreve a anterior: arquivo -> cofre local -> ambiente.
        private static readonly IConfiguration _configuracao = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .AddUserSecrets(typeof(Database).Assembly, optional: true)
            .AddEnvironmentVariables()
            .Build();

        // Falha imediatamente se nada foi configurado, indicando o que fazer.
        private static string ConnectionString =>
            _configuracao.GetConnectionString("Oracle")
            ?? throw new InvalidOperationException(
                "A connection string 'Oracle' não foi configurada.\n" +
                "Na pasta do projeto GestorRecursos.Core, execute:\n" +
                "  dotnet user-secrets set \"ConnectionStrings:Oracle\" \"User Id=...;Password=...;Data Source=localhost:1521/XEPDB1;\"");

        // Método que retorna uma nova conexão pronta para uso
        public static OracleConnection GetConnection()
        {
            return new OracleConnection(ConnectionString);
        }
    }
}
