using GestorRecursos.Core.Exportacao;
using GestorRecursos.Core.Models;
using GestorRecursos.Core.Validacao;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: os anexos do cliente.
//
// O documento mora na coluna CONTEUDO, dentro do banco. Não há serviço de
// armazenamento, nem credencial a mais, nem linha que sobrevive ao arquivo: o anexo
// existe por inteiro ou não existe.
public class ArquivoDAO
{
    // Sem CONTEUDO de propósito. A lista da ficha mostra nome, tipo e tamanho; trazer
    // o BLOB aqui seria baixar megabytes de documento para desenhar uma tabela.
    private const string Colunas =
        "ID, CLIENTE_ID, INSTANCIA_ID, TIPO, NOME_ARQUIVO, TAMANHO, DATA_UPLOAD";

    /// <summary>Os anexos do cliente, os dele e os das fases dos recursos dele.</summary>
    public async Task<List<Arquivo>> ListarDoClienteAsync(
        int clienteId,
        CancellationToken cancelamento = default)
    {
        const string sql = $"""
            SELECT {Colunas}
              FROM ARQUIVOS
             WHERE CLIENTE_ID = :clienteId
             ORDER BY DATA_UPLOAD DESC, ID DESC
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("clienteId", clienteId);

        var anexos = new List<Arquivo>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);

        while (await reader.ReadAsync(cancelamento))
        {
            anexos.Add(Ler(reader));
        }

        return anexos;
    }

    /// <summary>Todos os anexos do sistema, com o nome do cliente, para a exportação em massa.</summary>
    /// <remarks>
    /// Sem o conteúdo: a lista existe para montar as pastas e medir o tamanho antes de
    /// começar. Cada documento é lido um por vez, na hora de gravar.
    ///
    /// A ordem é a das pastas que serão criadas, para o andamento na tela avançar
    /// cliente por cliente em vez de pular de um lado para o outro.
    /// </remarks>
    public async Task<List<DocumentoParaExportar>> ListarTudoAsync(
        CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT a.ID, a.CLIENTE_ID, c.NOME, a.TIPO, a.NOME_ARQUIVO, a.TAMANHO
              FROM ARQUIVOS a
              JOIN CLIENTES c ON c.ID = a.CLIENTE_ID
             ORDER BY c.NOME, c.ID, a.TIPO, a.DATA_UPLOAD, a.ID
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);

        var documentos = new List<DocumentoParaExportar>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);

        while (await reader.ReadAsync(cancelamento))
        {
            documentos.Add(new DocumentoParaExportar(
                reader.Inteiro(0),
                reader.Inteiro(1),
                reader.Texto(2),
                ValorBanco.De<TipoArquivo>(reader.Texto(3)),
                reader.Texto(4),
                reader.Inteiro(5)));
        }

        return documentos;
    }

    /// <summary>Os dados de um anexo. Nulo quando a linha já foi apagada por outra janela.</summary>
    public async Task<Arquivo?> BuscarAsync(int id, CancellationToken cancelamento = default)
    {
        const string sql = $"SELECT {Colunas} FROM ARQUIVOS WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", id);

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        return await reader.ReadAsync(cancelamento) ? Ler(reader) : null;
    }

    /// <summary>O documento em si. Nulo quando a linha não existe mais.</summary>
    /// <remarks>
    /// Só aqui o BLOB sai do banco, e só quando alguém manda abrir ou exportar. É uma
    /// chamada separada justamente para que listar a ficha não pague esse preço.
    /// </remarks>
    public async Task<byte[]?> LerConteudoAsync(int id, CancellationToken cancelamento = default)
    {
        const string sql = "SELECT CONTEUDO FROM ARQUIVOS WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", id);

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);

        if (!await reader.ReadAsync(cancelamento) || reader.IsDBNull(0))
        {
            return null;
        }

        await using var blob = reader.GetOracleBlob(0);
        var conteudo = new byte[blob.Length];
        await blob.ReadExactlyAsync(conteudo, cancelamento);

        return conteudo;
    }

    /// <summary>Grava o documento e os dados dele, numa linha só.</summary>
    /// <remarks>
    /// Uma operação, uma tabela: ou o anexo entra inteiro, ou nada entra. Era o ganho
    /// de guardar o documento no banco — com o arquivo fora, enviar e registrar eram
    /// dois passos, e falhar no segundo deixava a ficha prometendo o que não abria.
    /// </remarks>
    public async Task<int> RegistrarAsync(
        Arquivo arquivo,
        byte[] conteudo,
        CancellationToken cancelamento = default)
    {
        // A conferência vale também aqui, e não só na tela: a API grava na mesma tabela.
        // Sem isto, a recusa viria do CHECK do banco, com texto de erro do Oracle.
        var conferencia = DocumentoAnexo.Conferir(conteudo, arquivo.NomeArquivo);
        if (!conferencia.Aceito)
        {
            throw new ArgumentException(conferencia.Problema, nameof(conteudo));
        }

        const string sql = """
            INSERT INTO ARQUIVOS
                   (CLIENTE_ID, INSTANCIA_ID, TIPO, NOME_ARQUIVO, CONTEUDO, TAMANHO, DATA_UPLOAD)
            VALUES (:clienteId, :instanciaId, :tipo, :nomeArquivo, :conteudo, :tamanho, :dataUpload)
            RETURNING ID INTO :novoId
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("clienteId", arquivo.ClienteId)
           .Param("instanciaId", arquivo.InstanciaId)
           .Param("tipo", arquivo.Tipo.Texto())
           .Param("nomeArquivo", arquivo.NomeArquivo)
           .Param("tamanho", conteudo.Length)
           .Param("dataUpload", arquivo.DataUpload == default ? DateTime.Now : arquivo.DataUpload);

        // O BLOB entra com o tipo declarado. Pelo caminho comum o driver trataria o
        // byte[] como RAW, que estoura em 32 KB — um PDF nem começaria a caber.
        cmd.Parameters.Add(new OracleParameter("conteudo", OracleDbType.Blob) { Value = conteudo });

        var pId = new OracleParameter("novoId", OracleDbType.Int32)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        cmd.Parameters.Add(pId);

        await cmd.ExecuteNonQueryAsync(cancelamento);

        arquivo.Id = Convert.ToInt32(pId.Value.ToString());
        arquivo.Tamanho = conteudo.Length;
        return arquivo.Id;
    }

    /// <summary>Apaga o anexo. Falso quando não havia nada para apagar.</summary>
    /// <remarks>
    /// O documento sai junto com a linha, porque está nela. Não sobra arquivo órfão
    /// para limpar depois.
    /// </remarks>
    public async Task<bool> ApagarAsync(int id, CancellationToken cancelamento = default)
    {
        const string sql = "DELETE FROM ARQUIVOS WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", id);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    private static Arquivo Ler(OracleDataReader reader) => new()
    {
        Id = reader.Inteiro(0),
        ClienteId = reader.Inteiro(1),
        InstanciaId = reader.InteiroOuNulo(2),
        Tipo = ValorBanco.De<TipoArquivo>(reader.Texto(3)),
        NomeArquivo = reader.Texto(4),
        Tamanho = reader.Inteiro(5),
        DataUpload = reader.Data(6)
    };
}
