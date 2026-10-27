using System.Text;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: tudo que conversa diretamente com o Oracle.
public static class Database
{
    // A lista de fontes mora em Configuracoes: duas listas divergiriam na ordem,
    // que é o que decide qual valor vence.
    private static IConfiguration Configuracao => Configuracoes.Atual;

    // Montada uma vez: a configuração da wallet é estática no driver e não pode
    // ser aplicada a cada conexão.
    //
    // Preguiçosa de propósito. Em campo inicializado direto, a falta de configuração
    // estourava dentro do construtor estático, e o que chegava à tela era
    // "The type initializer for 'Database' threw an exception" — a mensagem útil ficava
    // escondida na InnerException. Assim o erro sobe com o texto que explica o problema.
    private static readonly Lazy<string> StringConexao = new(PrepararConexao);

    /// <summary>Abre uma conexão já pronta para uso.</summary>
    /// <remarks>
    /// Assíncrono porque o banco está na nuvem: cada ida à rede custa dezenas de
    /// milissegundos, e segurar a thread nesse tempo trava a interface.
    /// Quem chama é dono da conexão e deve descartá-la (<c>await using</c>).
    /// </remarks>
    public static async Task<OracleConnection> AbrirAsync(CancellationToken cancelamento = default)
    {
        var conexao = new OracleConnection(StringConexao.Value);
        try
        {
            await conexao.OpenAsync(cancelamento);
            await ApontarEsquemaAsync(conexao, cancelamento);
            return conexao;
        }
        catch
        {
            // Sem isto, uma falha na abertura deixaria a conexão pendurada.
            await conexao.DisposeAsync();
            throw;
        }
    }

    /// <summary>Diz ao banco em que esquema procurar as tabelas sem dono escrito.</summary>
    /// <remarks>
    /// Nas estações, o usuário do banco não é o dono do esquema: ele só lê e grava, e não
    /// pode alterar nem apagar estrutura. Como ele não tem tabela própria, um
    /// <c>FROM CLIENTES</c> não acharia nada.
    ///
    /// Isto resolve sem tocar numa linha de SQL do sistema, e sem encher o outro esquema
    /// de sinônimos — que, além de serem dezenas, exigiriam um privilégio amplo
    /// (<c>CREATE ANY SYNONYM</c>) só para criá-los.
    ///
    /// Em desenvolvimento a chave fica vazia e nada acontece: quem conecta já é o dono.
    /// </remarks>
    private static async Task ApontarEsquemaAsync(
        OracleConnection conexao,
        CancellationToken cancelamento)
    {
        var esquema = Configuracao["Oracle:Esquema"];
        if (string.IsNullOrWhiteSpace(esquema))
        {
            return;
        }

        // Nome de objeto não entra por parâmetro; por isso só passam letras, números e
        // sublinhado — o resto nem chega a virar comando.
        if (!esquema.All(c => char.IsAsciiLetterOrDigit(c) || c == '_'))
        {
            throw new InvalidOperationException(
                $"O esquema configurado tem caractere inválido: {esquema}");
        }

        await using var cmd = conexao.CreateCommand();
        cmd.CommandText = $"ALTER SESSION SET CURRENT_SCHEMA = {esquema.ToUpperInvariant()}";
        await cmd.ExecuteNonQueryAsync(cancelamento);
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
        // A mensagem serve a duas pessoas: quem só abriu o programa e quem vai consertar.
        // A primeira frase é para ela; o resto, para quem instalou.
        var bruta = Configuracao.GetConnectionString("Oracle")
            ?? throw new InvalidOperationException(
                "O programa não encontrou a configuração do banco de dados. "
                + "Se o sistema acabou de ser instalado, avise quem fez a instalação.\n\n"
                + "Para quem instala: defina a connection string 'Oracle' em "
                + $"appsettings.json (na pasta {AppContext.BaseDirectory}), nos User Secrets "
                + "ou na variável de ambiente ConnectionStrings__Oracle.");

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

        // Caminho relativo conta a partir da pasta do programa, e não de onde ele foi
        // chamado. É o que permite o instalador gravar "wallet" e funcionar em qualquer
        // pasta de instalação — um atalho do menu Iniciar não garante o diretório atual.
        tnsAdmin = CaminhoDoPrograma(tnsAdmin);
        walletLocation = CaminhoDoPrograma(walletLocation);

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

    private static string? CaminhoDoPrograma(string? caminho) =>
        string.IsNullOrWhiteSpace(caminho) || Path.IsPathRooted(caminho)
            ? caminho
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, caminho));
}
