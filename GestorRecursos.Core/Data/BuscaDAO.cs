using GestorRecursos.Core.Models;
using GestorRecursos.Core.Validacao;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: a busca global.
//
// Existe por causa da frase que abriu o projeto: "recebo atualização de um
// processo e não acho mais o cliente; às vezes volta a placa, mas eu não sei
// quem é o dono". Aqui qualquer ponta leva ao cliente.
public class BuscaDAO
{
    /// <summary>Procura o termo em cliente, veículo, multa e processo de CNH.</summary>
    /// <remarks>
    /// Uma consulta só, com UNION ALL, em vez de quatro idas ao banco. O termo é
    /// comparado em maiúsculas contra o índice funcional de nome, e por igualdade
    /// exata nos campos que são códigos (CPF, placa, AIT, processo) — quem digita
    /// uma placa quer aquela placa, não as parecidas.
    /// </remarks>
    public async Task<List<ResultadoBusca>> ProcurarAsync(
        string termo,
        int limite = 30,
        CancellationToken cancelamento = default)
    {
        if (string.IsNullOrWhiteSpace(termo))
        {
            return [];
        }

        termo = termo.Trim();
        var digitos = Normalizacao.SomenteDigitos(termo);
        var termoMaiusculo = termo.ToUpperInvariant();

        // WhatsApp aceita pedaço do número: ela costuma lembrar do final, não do DDD. Os
        // dígitos vêm sem pontuação dos dois lados, então o telefone digitado com máscara
        // casa igual. Abaixo de quatro dígitos a busca não vale a pena — "47" traria a
        // cidade inteira —, e aí o valor impossível desliga a condição.
        var telefone = digitos.Length >= 4 ? $"%{digitos}%" : "\u0000";

        const string sql = """
            SELECT * FROM (
                SELECT 'CLIENTE' AS ORIGEM, C.NOME AS ENCONTRADO, C.ID AS CLIENTE_ID,
                       C.NOME AS CLIENTE_NOME, C.CPF, C.WHATSAPP, C.ARQUIVADO,
                       NULL AS DETALHE
                  FROM CLIENTES C
                 WHERE UPPER(C.NOME) LIKE '%' || :termo || '%'
                    OR C.CPF = :digitos
                    OR C.WHATSAPP LIKE :telefone

                UNION ALL

                SELECT 'VEICULO', V.PLACA, C.ID, C.NOME, C.CPF, C.WHATSAPP, C.ARQUIVADO,
                       'Veiculo ' || V.PLACA || NVL2(V.MARCA_MODELO, ' - ' || V.MARCA_MODELO, '')
                       || ' (' || LOWER(CV.VINCULO) || ')'
                  FROM VEICULOS V
                  JOIN CLIENTE_VEICULO CV ON CV.VEICULO_ID = V.ID
                  JOIN CLIENTES C ON C.ID = CV.CLIENTE_ID
                 WHERE V.PLACA = :termo OR V.RENAVAM = :digitos

                UNION ALL

                SELECT 'MULTA', M.AIT, C.ID, C.NOME, C.CPF, C.WHATSAPP, C.ARQUIVADO,
                       'AIT ' || M.AIT || ' - placa ' || V.PLACA
                       || NVL2(M.PRAZO_DEFESA, ' - prazo ' || TO_CHAR(M.PRAZO_DEFESA, 'DD/MM/YYYY'), '')
                  FROM MULTAS M
                  JOIN VEICULOS V ON V.ID = M.VEICULO_ID
                  JOIN CLIENTE_VEICULO CV ON CV.VEICULO_ID = V.ID
                  JOIN CLIENTES C ON C.ID = CV.CLIENTE_ID
                 WHERE M.AIT = :termo

                UNION ALL

                SELECT 'PROCESSO', P.NUMERO_PROCESSO, C.ID, C.NOME, C.CPF, C.WHATSAPP, C.ARQUIVADO,
                       INITCAP(P.TIPO) || ' - processo ' || P.NUMERO_PROCESSO
                       || NVL2(P.PRAZO_DEFESA, ' - prazo ' || TO_CHAR(P.PRAZO_DEFESA, 'DD/MM/YYYY'), '')
                  FROM PROCESSOS_CNH P
                  JOIN CLIENTES C ON C.ID = P.CLIENTE_ID
                 WHERE P.NUMERO_PROCESSO = :termo

                UNION ALL

                SELECT 'PROTOCOLO', I.NUMERO_PROTOCOLO, C.ID, C.NOME, C.CPF, C.WHATSAPP, C.ARQUIVADO,
                       'Protocolo ' || I.NUMERO_PROTOCOLO || ' - ' || INITCAP(I.FASE)
                  FROM INSTANCIAS I
                  JOIN ITENS_CONTRATACAO IT ON IT.ID = I.ITEM_ID
                  JOIN CONTRATACOES CT ON CT.ID = IT.CONTRATACAO_ID
                  JOIN CLIENTES C ON C.ID = CT.CLIENTE_ID
                 WHERE I.NUMERO_PROTOCOLO = :termo
            )
            ORDER BY ORIGEM, CLIENTE_NOME
            FETCH FIRST :limite ROWS ONLY
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("termo", termoMaiusculo)
           .Param("digitos", string.IsNullOrEmpty(digitos) ? "\u0000" : digitos)
           .Param("telefone", telefone)
           .Param("limite", limite);

        var lista = new List<ResultadoBusca>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(new ResultadoBusca
            {
                Origem = reader.Texto(0),
                Encontrado = reader.Texto(1),
                ClienteId = reader.Inteiro(2),
                ClienteNome = reader.Texto(3),
                ClienteCpf = reader.Texto(4),
                ClienteWhatsapp = reader.TextoOuNulo(5),
                ClienteArquivado = reader.Booleano(6),
                Detalhe = reader.TextoOuNulo(7)
            });
        }

        return lista;
    }
}
