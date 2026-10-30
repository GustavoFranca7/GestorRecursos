using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: as duas listas da visão do dia.
public class PainelDAO
{
    // Um prazo por linha, com quem é e do que se trata. As junções à esquerda são as
    // duas origens possíveis do recurso — uma multa ou um processo de CNH —, e só uma
    // delas existe por item (CK_ITENS_ORIGEM_UNICA).
    private const string SqlPrazos = $"""
        SELECT I.ID, CO.CLIENTE_ID, CL.NOME, CL.WHATSAPP,
               I.PRAZO_PROTOCOLO, I.FASE, TS.NOME AS SERVICO, I.ORGAO_DESTINATARIO,
               NVL(M.AIT, PC.NUMERO_PROCESSO) AS ORIGEM,
               V.PLACA
          FROM INSTANCIAS I
          JOIN ITENS_CONTRATACAO IT  ON IT.ID = I.ITEM_ID
          JOIN CONTRATACOES CO       ON CO.ID = IT.CONTRATACAO_ID
          JOIN CLIENTES CL           ON CL.ID = CO.CLIENTE_ID
          JOIN TIPOS_SERVICO TS      ON TS.ID = IT.TIPO_SERVICO_ID
          LEFT JOIN MULTAS M         ON M.ID  = IT.MULTA_ID
          LEFT JOIN VEICULOS V       ON V.ID  = M.VEICULO_ID
          LEFT JOIN PROCESSOS_CNH PC ON PC.ID = IT.PROCESSO_CNH_ID
         WHERE I.DATA_PROTOCOLO IS NULL
           AND I.PRAZO_PROTOCOLO IS NOT NULL
           AND I.SITUACAO NOT IN {SituacoesSql.InstanciaEncerrada}
           AND IT.SITUACAO <> 'CANCELADO'
           AND CO.SITUACAO <> 'CANCELADO'
           AND CL.ARQUIVADO = 'N'
         ORDER BY I.PRAZO_PROTOCOLO, CL.NOME
        """;

    // A view já sabe quem está devendo; aqui só se tira o cliente arquivado, para
    // combinar com a aba "Com pendência" da lista de clientes.
    private const string SqlCobrancas = """
        SELECT V.CONTRATACAO_ID, V.CLIENTE_ID, V.CLIENTE_NOME, V.CLIENTE_WHATSAPP,
               V.DATA_CONTRATACAO, V.VALOR_FINAL, V.TOTAL_PAGO, V.EM_ABERTO,
               V.DIAS_DESDE_CONTRATACAO
          FROM VW_COBRANCAS_ABERTAS V
          JOIN CLIENTES CL ON CL.ID = V.CLIENTE_ID
         WHERE CL.ARQUIVADO = 'N'
         ORDER BY V.DIAS_DESDE_CONTRATACAO DESC, V.EM_ABERTO DESC
        """;

    /// <summary>Monta a visão do dia inteira: prazos abertos e cobranças em aberto.</summary>
    /// <remarks>
    /// Traz todos os prazos abertos, e não só os da semana: a filtragem é na tela. São
    /// dezenas de linhas, não milhares, e assim trocar de aba não volta ao banco.
    /// </remarks>
    public async Task<VisaoDoDia> MontarAsync(CancellationToken cancelamento = default)
    {
        await using var conexao = await Database.AbrirAsync(cancelamento);

        var prazos = new List<PrazoDoDia>();
        await using (var cmd = conexao.Comando(SqlPrazos))
        {
            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                prazos.Add(new PrazoDoDia
                {
                    InstanciaId = reader.Inteiro(0),
                    ClienteId = reader.Inteiro(1),
                    ClienteNome = reader.Texto(2),
                    ClienteWhatsapp = reader.Texto(3),
                    Prazo = reader.Data(4),
                    Fase = reader.Texto(5),
                    Servico = reader.Texto(6),
                    Orgao = reader.TextoOuNulo(7),
                    Origem = reader.TextoOuNulo(8),
                    Placa = reader.TextoOuNulo(9)
                });
            }
        }

        var cobrancas = new List<CobrancaDoDia>();
        await using (var cmd = conexao.Comando(SqlCobrancas))
        {
            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                cobrancas.Add(new CobrancaDoDia
                {
                    ContratacaoId = reader.Inteiro(0),
                    ClienteId = reader.Inteiro(1),
                    ClienteNome = reader.Texto(2),
                    ClienteWhatsapp = reader.Texto(3),
                    DataContratacao = reader.Data(4),
                    ValorFinal = reader.Decimal(5),
                    TotalPago = reader.Decimal(6),
                    EmAberto = reader.Decimal(7),
                    Dias = reader.Inteiro(8)
                });
            }
        }

        return new VisaoDoDia { Prazos = prazos, Cobrancas = cobrancas };
    }
}
