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
    /// <summary>Nome do ambiente, quando não é o de produção.</summary>
    /// <remarks>
    /// Produção não se anuncia: a chave fica vazia lá, e nada aparece na tela. Em
    /// qualquer outro lugar ela é preenchida e vira um aviso visível — o engano caro é
    /// achar que se está no teste quando se está no banco real.
    /// </remarks>
    public static string? Ambiente
    {
        get
        {
            var nome = Atual["Ambiente"];
            return string.IsNullOrWhiteSpace(nome) ? null : nome.Trim();
        }
    }

    /// <summary>O ambiente anunciado mexe com dado real: o selo vira aviso, não rótulo.</summary>
    public static bool AmbienteDelicado =>
        Ambiente?.Contains("PRODU", StringComparison.OrdinalIgnoreCase) == true;

    public static IConfiguration Atual { get; } = Montar();

    private static IConfiguration Montar()
    {
        var fontes = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true);

#if DEBUG
        // O cofre local só existe em desenvolvimento, e de propósito.
        //
        // Ele fica em %APPDATA% do usuário do Windows, não na pasta do programa: valeria
        // para qualquer cópia rodando na mesma máquina, inclusive a instalada. Sem esta
        // trava, o programa instalado na máquina de quem desenvolve leria o
        // appsettings.json e logo depois seria sobrescrito pelo cofre — os dois ambientes
        // se cruzariam, e o aviso de ambiente na tela mentiria.
        //
        // A versão publicada sai em Release e enxerga apenas o appsettings.json ao lado
        // do executável.
        fontes.AddUserSecrets(typeof(Configuracoes).Assembly, optional: true);
#endif

        return fontes.AddEnvironmentVariables().Build();
    }
}
