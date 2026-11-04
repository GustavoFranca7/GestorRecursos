using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: os dados da despachante que assina os documentos (RF-51).
public class EscritorioDAO
{
    private const string Colunas = """
        NOME, NACIONALIDADE, ESTADO_CIVIL, PROFISSAO, RG, RG_ORGAO, CPF,
        REGISTRO_PROFISSIONAL, EMAIL, LOGRADOURO, NUMERO, COMPLEMENTO, BAIRRO,
        CEP, CIDADE, UF, CIDADE_ASSINATURA
        """;

    /// <summary>Devolve o único registro, ou nulo quando o banco ainda não foi preenchido.</summary>
    /// <remarks>
    /// Nulo não é erro: o script de dados do escritório roda fora do repositório, e uma
    /// instalação nova pode não tê-lo rodado ainda. A tela avisa, em vez de quebrar.
    /// </remarks>
    public async Task<Escritorio?> BuscarAsync(CancellationToken cancelamento = default)
    {
        var sql = $"SELECT {Colunas} FROM ESCRITORIO WHERE ID = 1";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        return await reader.ReadAsync(cancelamento) ? Ler(reader) : null;
    }

    /// <summary>Grava os dados da despachante. A linha é uma só, travada em ID = 1.</summary>
    /// <remarks>
    /// MERGE, e não UPDATE: numa instalação cuja carga inicial não rodou, a tabela está
    /// vazia, e um UPDATE "daria certo" sem gravar nada — a tela diria salvo e a
    /// procuração continuaria saindo sem a outorgada.
    /// </remarks>
    public async Task AtualizarAsync(Escritorio escritorio, CancellationToken cancelamento = default)
    {
        const string sql = """
            MERGE INTO ESCRITORIO alvo
            USING (SELECT 1 AS ID FROM DUAL) origem
               ON (alvo.ID = origem.ID)
            WHEN MATCHED THEN UPDATE SET
                 NOME = :nome, NACIONALIDADE = :nacionalidade, ESTADO_CIVIL = :estadoCivil,
                 PROFISSAO = :profissao, RG = :rg, RG_ORGAO = :rgOrgao, CPF = :cpf,
                 REGISTRO_PROFISSIONAL = :registro, EMAIL = :email,
                 LOGRADOURO = :logradouro, NUMERO = :numero, COMPLEMENTO = :complemento,
                 BAIRRO = :bairro, CEP = :cep, CIDADE = :cidade, UF = :uf,
                 CIDADE_ASSINATURA = :cidadeAssinatura
            WHEN NOT MATCHED THEN INSERT
                 (ID, NOME, NACIONALIDADE, ESTADO_CIVIL, PROFISSAO, RG, RG_ORGAO, CPF,
                  REGISTRO_PROFISSIONAL, EMAIL, LOGRADOURO, NUMERO, COMPLEMENTO, BAIRRO,
                  CEP, CIDADE, UF, CIDADE_ASSINATURA)
            VALUES (1, :nome, :nacionalidade, :estadoCivil, :profissao, :rg, :rgOrgao, :cpf,
                    :registro, :email, :logradouro, :numero, :complemento, :bairro,
                    :cep, :cidade, :uf, :cidadeAssinatura)
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);

        cmd.Param("nome", escritorio.Nome.Trim())
           .Param("nacionalidade", Limpo(escritorio.Nacionalidade))
           .Param("estadoCivil", Limpo(escritorio.EstadoCivil))
           .Param("profissao", Limpo(escritorio.Profissao))
           .Param("rg", Limpo(escritorio.Rg))
           .Param("rgOrgao", Limpo(escritorio.RgOrgao))
           .Param("cpf", Limpo(escritorio.Cpf))
           .Param("registro", Limpo(escritorio.RegistroProfissional))
           .Param("email", Limpo(escritorio.Email))
           .Param("logradouro", Limpo(escritorio.Logradouro))
           .Param("numero", Limpo(escritorio.Numero))
           .Param("complemento", Limpo(escritorio.Complemento))
           .Param("bairro", Limpo(escritorio.Bairro))
           .Param("cep", Limpo(escritorio.Cep))
           .Param("cidade", Limpo(escritorio.Cidade))
           .Param("uf", Limpo(escritorio.Uf))
           .Param("cidadeAssinatura", Limpo(escritorio.CidadeAssinatura));

        await cmd.ExecuteNonQueryAsync(cancelamento);
    }

    // Campo em branco vira nulo: o resto do sistema ja trata nulo como "nao informado",
    // e uma string vazia faria a procuracao imprimir espaco onde deveria faltar dado.
    private static string? Limpo(string? texto) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static Escritorio Ler(OracleDataReader reader) => new()
    {
        Nome = reader.Texto(0),
        Nacionalidade = reader.TextoOuNulo(1),
        EstadoCivil = reader.TextoOuNulo(2),
        Profissao = reader.TextoOuNulo(3),
        Rg = reader.TextoOuNulo(4),
        RgOrgao = reader.TextoOuNulo(5),
        Cpf = reader.TextoOuNulo(6),
        RegistroProfissional = reader.TextoOuNulo(7),
        Email = reader.TextoOuNulo(8),
        Logradouro = reader.TextoOuNulo(9),
        Numero = reader.TextoOuNulo(10),
        Complemento = reader.TextoOuNulo(11),
        Bairro = reader.TextoOuNulo(12),
        Cep = reader.TextoOuNulo(13),
        Cidade = reader.TextoOuNulo(14),
        Uf = reader.TextoOuNulo(15),
        CidadeAssinatura = reader.TextoOuNulo(16)
    };
}
