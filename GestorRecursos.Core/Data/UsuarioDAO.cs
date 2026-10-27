using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: quem entra no sistema.
public class UsuarioDAO
{
    private const string Colunas = """
        ID, NOME, LOGIN, SENHA_HASH, ATIVO, EMAIL_RECUPERACAO,
        CODIGO_HASH, CODIGO_EXPIRA_EM, CODIGO_TENTATIVAS, ADMINISTRADOR
        """;

    /// <summary>Quantos acessos existem. Zero significa instalação nova.</summary>
    public async Task<int> ContarAsync(CancellationToken cancelamento = default)
    {
        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando("SELECT COUNT(*) FROM USUARIOS");

        var total = await cmd.ExecuteScalarAsync(cancelamento);
        return Convert.ToInt32(total);
    }

    /// <summary>Todos os acessos, para a aba Usuários. Inativo também aparece.</summary>
    public async Task<List<Usuario>> ListarAsync(CancellationToken cancelamento = default)
    {
        var sql = $"SELECT {Colunas} FROM USUARIOS ORDER BY ATIVO DESC, NOME";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);

        var lista = new List<Usuario>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(Ler(reader));
        }

        return lista;
    }

    /// <summary>Liga e desliga o acesso. Desligar não apaga (RNF-03).</summary>
    public async Task MudarAtivoAsync(int id, bool ativo, CancellationToken cancelamento = default)
    {
        const string sql = "UPDATE USUARIOS SET ATIVO = :ativo WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("ativo", ativo.ParaSN()).Param("id", id);

        await cmd.ExecuteNonQueryAsync(cancelamento);
    }

    /// <summary>Devolve o acesso a uma senha provisória.</summary>
    /// <remarks>
    /// Limpa o e-mail de propósito: sem ele, a pessoa volta a cair na janela de primeiro
    /// acesso e é obrigada a escolher a própria senha. Assim o administrador devolve a
    /// entrada sem passar a saber a senha definitiva de ninguém.
    /// </remarks>
    public async Task ResetarAsync(int id, string senhaHash, CancellationToken cancelamento = default)
    {
        const string sql = """
            UPDATE USUARIOS
               SET SENHA_HASH = :senha,
                   EMAIL_RECUPERACAO = NULL,
                   CODIGO_HASH = NULL,
                   CODIGO_EXPIRA_EM = NULL,
                   CODIGO_TENTATIVAS = 0
             WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("senha", senhaHash).Param("id", id);

        await cmd.ExecuteNonQueryAsync(cancelamento);
    }

    /// <summary>Acha pelo login, sem distinguir maiúscula de minúscula.</summary>
    /// <remarks>
    /// Devolve também o inativo: quem foi desligado merece ouvir que o acesso está
    /// desativado, e não que a senha está errada.
    /// </remarks>
    public async Task<Usuario?> BuscarPorLoginAsync(string login, CancellationToken cancelamento = default)
    {
        var sql = $"SELECT {Colunas} FROM USUARIOS WHERE UPPER(LOGIN) = UPPER(:login)";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("login", login?.Trim() ?? string.Empty);

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        return await reader.ReadAsync(cancelamento) ? Ler(reader) : null;
    }

    /// <summary>Cria um acesso. A senha chega já embaralhada.</summary>
    public async Task<int> InserirAsync(Usuario usuario, CancellationToken cancelamento = default)
    {
        const string sql = """
            INSERT INTO USUARIOS (NOME, LOGIN, SENHA_HASH, ATIVO, EMAIL_RECUPERACAO, ADMINISTRADOR)
            VALUES (:nome, :login, :senha, 'S', :email, :adm)
            RETURNING ID INTO :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("nome", usuario.Nome.Trim())
           .Param("login", usuario.Login.Trim())
           .Param("senha", usuario.SenhaHash)
           .Param("email", usuario.EmailRecuperacao)
           .Param("adm", usuario.Administrador.ParaSN());

        var id = new OracleParameter("id", OracleDbType.Int32)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        cmd.Parameters.Add(id);

        await cmd.ExecuteNonQueryAsync(cancelamento);
        return int.Parse(id.Value?.ToString() ?? "0");
    }

    /// <summary>Guarda o e-mail do primeiro acesso.</summary>
    public async Task DefinirEmailAsync(int id, string email, CancellationToken cancelamento = default)
    {
        const string sql = "UPDATE USUARIOS SET EMAIL_RECUPERACAO = :email WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("email", email.Trim()).Param("id", id);

        await cmd.ExecuteNonQueryAsync(cancelamento);
    }

    /// <summary>Grava o código de recuperação e zera as tentativas.</summary>
    public async Task GravarCodigoAsync(
        int id,
        string codigoHash,
        DateTime expiraEm,
        CancellationToken cancelamento = default)
    {
        const string sql = """
            UPDATE USUARIOS
               SET CODIGO_HASH = :hash,
                   CODIGO_EXPIRA_EM = :expira,
                   CODIGO_TENTATIVAS = 0
             WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("hash", codigoHash).Param("expira", expiraEm).Param("id", id);

        await cmd.ExecuteNonQueryAsync(cancelamento);
    }

    /// <summary>Conta mais um erro no código e devolve o total.</summary>
    public async Task<int> ErrouCodigoAsync(int id, CancellationToken cancelamento = default)
    {
        const string sql = """
            UPDATE USUARIOS SET CODIGO_TENTATIVAS = CODIGO_TENTATIVAS + 1
             WHERE ID = :id
            RETURNING CODIGO_TENTATIVAS INTO :total
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", id);

        var total = new OracleParameter("total", OracleDbType.Int32)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        cmd.Parameters.Add(total);

        await cmd.ExecuteNonQueryAsync(cancelamento);
        return int.Parse(total.Value?.ToString() ?? "0");
    }

    /// <summary>Troca a senha e apaga o código, numa operação só.</summary>
    /// <remarks>
    /// Juntos de propósito: código que sobrevive à troca de senha continua valendo para
    /// trocar de novo, e aí quem o interceptou volta quando quiser.
    /// </remarks>
    public async Task TrocarSenhaAsync(int id, string senhaHash, CancellationToken cancelamento = default)
    {
        const string sql = """
            UPDATE USUARIOS
               SET SENHA_HASH = :senha,
                   CODIGO_HASH = NULL,
                   CODIGO_EXPIRA_EM = NULL,
                   CODIGO_TENTATIVAS = 0
             WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("senha", senhaHash).Param("id", id);

        await cmd.ExecuteNonQueryAsync(cancelamento);
    }

    /// <summary>Queima o código depois de errar demais.</summary>
    public async Task InvalidarCodigoAsync(int id, CancellationToken cancelamento = default)
    {
        const string sql = """
            UPDATE USUARIOS
               SET CODIGO_HASH = NULL, CODIGO_EXPIRA_EM = NULL, CODIGO_TENTATIVAS = 0
             WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", id);

        await cmd.ExecuteNonQueryAsync(cancelamento);
    }

    private static Usuario Ler(OracleDataReader reader) => new()
    {
        Id = reader.Inteiro(0),
        Nome = reader.Texto(1),
        Login = reader.Texto(2),
        SenhaHash = reader.Texto(3),
        Ativo = reader.Booleano(4),
        EmailRecuperacao = reader.TextoOuNulo(5),
        CodigoHash = reader.TextoOuNulo(6),
        CodigoExpiraEm = reader.DataOuNulo(7),
        CodigoTentativas = reader.Inteiro(8),
        Administrador = reader.Booleano(9)
    };
}
