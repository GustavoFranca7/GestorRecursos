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
