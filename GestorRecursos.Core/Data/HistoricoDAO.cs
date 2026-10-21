using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: a linha do tempo do cliente, montada do que já existe.
public class HistoricoDAO
{
    // Um UNION ALL, e não uma consulta por origem: a ficha abre contra um banco na nuvem,
    // e sete idas à rede para encher uma aba custariam mais que a consulta inteira.
    //
    // Todas as pernas devolvem as mesmas dez colunas, nesta ordem. A primeira declara os
    // tipos com CAST, porque é dela que o Oracle tira o formato das colunas nulas.
    private const string Sql = """
        WITH ITENS AS (
            SELECT I.ID,
                   T.NOME AS SERVICO,
                   CASE
                     WHEN M.AIT IS NOT NULL
                       THEN 'AIT ' || M.AIT || NVL2(V.PLACA, ' - placa ' || V.PLACA, '')
                     WHEN P.NUMERO_PROCESSO IS NOT NULL
                       THEN 'processo ' || P.NUMERO_PROCESSO
                   END AS IDENTIFICACAO
              FROM ITENS_CONTRATACAO I
              JOIN CONTRATACOES CT         ON CT.ID = I.CONTRATACAO_ID
              JOIN TIPOS_SERVICO T         ON T.ID  = I.TIPO_SERVICO_ID
              LEFT JOIN MULTAS M           ON M.ID  = I.MULTA_ID
              LEFT JOIN VEICULOS V         ON V.ID  = M.VEICULO_ID
              LEFT JOIN PROCESSOS_CNH P    ON P.ID  = I.PROCESSO_CNH_ID
             WHERE CT.CLIENTE_ID = :cliente
        )
        SELECT * FROM (
            SELECT 'CADASTRO'                       AS TIPO,
                   C.DATA_CADASTRO                  AS QUANDO,
                   CAST(NULL AS NUMBER)             AS VALOR,
                   CAST(NULL AS NUMBER)             AS TABELA,
                   CAST(NULL AS NUMBER)             AS DESCONTO,
                   CAST(NULL AS NUMBER)             AS QUANTIDADE,
                   CAST(NULL AS VARCHAR2(30))       AS CODIGO,
                   CAST(NULL AS VARCHAR2(20))       AS RESULTADO,
                   CAST(NULL AS VARCHAR2(300))      AS IDENTIFICACAO,
                   CAST(NULL AS VARCHAR2(600))      AS DETALHE
              FROM CLIENTES C
             WHERE C.ID = :cliente

            UNION ALL

            SELECT 'CONTRATACAO', CT.DATA_CONTRATACAO, CT.VALOR_FINAL,
                   CT.VALOR_TABELA, CT.DESCONTO,
                   (SELECT COUNT(*) FROM ITENS_CONTRATACAO I
                     WHERE I.CONTRATACAO_ID = CT.ID),
                   NULL, NULL, NULL,
                   (SELECT LISTAGG(DISTINCT T.NOME, ' - ')
                             WITHIN GROUP (ORDER BY T.NOME)
                      FROM ITENS_CONTRATACAO I
                      JOIN TIPOS_SERVICO T ON T.ID = I.TIPO_SERVICO_ID
                     WHERE I.CONTRATACAO_ID = CT.ID)
              FROM CONTRATACOES CT
             WHERE CT.CLIENTE_ID = :cliente

            UNION ALL

            SELECT 'PAGAMENTO', P.DATA_PAGAMENTO, P.VALOR, NULL, NULL, P.PARCELAS,
                   P.FORMA, NULL, NULL, P.OBSERVACAO
              FROM PAGAMENTOS P
              JOIN CONTRATACOES CT ON CT.ID = P.CONTRATACAO_ID
             WHERE CT.CLIENTE_ID = :cliente

            UNION ALL

            SELECT 'DESPESA', D.DATA_DESPESA, D.VALOR, NULL, NULL, NULL,
                   NULL, NULL, NULL, D.TIPO
              FROM DESPESAS D
              JOIN CONTRATACOES CT ON CT.ID = D.CONTRATACAO_ID
             WHERE CT.CLIENTE_ID = :cliente

            UNION ALL

            SELECT 'PROTOCOLO', I.DATA_PROTOCOLO, NULL, NULL, NULL, NULL,
                   I.FASE, NULL, IT.IDENTIFICACAO,
                   I.NUMERO_PROTOCOLO
                   || NVL2(I.ORGAO_DESTINATARIO, ' - ' || I.ORGAO_DESTINATARIO, '')
              FROM INSTANCIAS I
              JOIN ITENS IT ON IT.ID = I.ITEM_ID
             WHERE I.DATA_PROTOCOLO IS NOT NULL

            UNION ALL

            SELECT 'RESULTADO', I.DATA_RESULTADO, NULL, NULL, NULL, NULL,
                   I.FASE, I.RESULTADO, IT.IDENTIFICACAO, NULL
              FROM INSTANCIAS I
              JOIN ITENS IT ON IT.ID = I.ITEM_ID
             WHERE I.DATA_RESULTADO IS NOT NULL

            UNION ALL

            SELECT 'ETAPA', E.DATA_CONCLUSAO, NULL, NULL, NULL, NULL,
                   NULL, NULL, IT.IDENTIFICACAO,
                   EM.NOME || ' - ' || IT.SERVICO
              FROM ETAPAS E
              JOIN ITENS IT          ON IT.ID = E.ITEM_ID
              JOIN ETAPAS_MODELO EM  ON EM.ID = E.ETAPA_MODELO_ID
             WHERE E.CONCLUIDA = 'S' AND E.DATA_CONCLUSAO IS NOT NULL
        )
        ORDER BY QUANDO DESC, TIPO
        """;

    /// <summary>A linha do tempo do cliente, do mais recente para o mais antigo.</summary>
    public async Task<List<EventoDoCliente>> ListarAsync(
        int clienteId,
        CancellationToken cancelamento = default)
    {
        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(Sql);
        cmd.Param("cliente", clienteId);

        var lista = new List<EventoDoCliente>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(Ler(reader));
        }

        return lista;
    }

    private static EventoDoCliente Ler(OracleDataReader reader) => new()
    {
        Tipo = reader.Texto(0) switch
        {
            "CONTRATACAO" => TipoEvento.Contratacao,
            "PAGAMENTO" => TipoEvento.Pagamento,
            "DESPESA" => TipoEvento.Despesa,
            "PROTOCOLO" => TipoEvento.Protocolo,
            "RESULTADO" => TipoEvento.Resultado,
            "ETAPA" => TipoEvento.Etapa,
            _ => TipoEvento.Cadastro
        },
        Quando = reader.Data(1),
        Valor = reader.DecimalOuNulo(2),
        ValorTabela = reader.DecimalOuNulo(3),
        Desconto = reader.DecimalOuNulo(4),
        Quantidade = reader.InteiroOuNulo(5),
        Codigo = reader.TextoOuNulo(6),
        Resultado = reader.TextoOuNulo(7),
        Identificacao = reader.TextoOuNulo(8),
        Detalhe = reader.TextoOuNulo(9)
    };
}
