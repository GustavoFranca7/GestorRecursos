using Microsoft.Extensions.Configuration;

namespace GestorRecursos.Core;

/// <summary>De onde saem senha do banco, caminho da wallet e conta de e-mail.</summary>
/// <remarks>
/// Montada uma vez e compartilhada: antes cada parte que precisava de configuração
/// construía a sua, e duas listas de fontes acabariam divergindo na ordem — que é
/// justamente o que decide qual valor vence.
///
/// Cada fonte sobrescreve a anterior: arquivo, cofre local, ambiente. Nada disso
/// entra no código, para a senha não ir para o Git.
/// </remarks>
public static class Configuracoes
{
    public static IConfiguration Atual { get; } = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true)
        .AddUserSecrets(typeof(Configuracoes).Assembly, optional: true)
        .AddEnvironmentVariables()
        .Build();
}
