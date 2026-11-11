using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: os processos do escritório inteiro, para a tela de Processos.
//
// Fica fora do FichaClienteDAO porque a pergunta é outra: lá se parte de um cliente, aqui
// de nenhum. A consulta é parecida, mas filtrar por cliente e nao filtrar por nada sao
// planos de execucao diferentes, e misturar os dois num metodo so esconderia isso.
public class ProcessoDAO
{
    // Um item contratado por linha, com o cliente junto. Cliente arquivado fica de fora:
    // quem saiu da lista de clientes nao deveria reaparecer no acompanhamento.
    private const string SqlProcessos = """
        SELECT IT.ID, IT.CONTRATACAO_ID, IT.SITUACAO, TS.NOME,
               M.AIT, M.ENQUADRAMENTO, M.GRAVIDADE, M.ORGAO_AUTUADOR,
               V.PLACA, P.NUMERO_PROCESSO,
               CT.DATA_CONTRATACAO, CT.VALOR_FINAL,
               (SELECT COUNT(*) FROM ITENS_CONTRATACAO X WHERE X.CONTRATACAO_ID = CT.ID),
               P.TIPO, IT.MULTA_ID, IT.PROCESSO_CNH_ID,
               CL.ID, CL.NOME, CL.CPF, CL.WHATSAPP
          FROM ITENS_CONTRATACAO IT
          JOIN CONTRATACOES CT  ON CT.ID = IT.CONTRATACAO_ID
          JOIN CLIENTES CL      ON CL.ID = CT.CLIENTE_ID
          JOIN TIPOS_SERVICO TS ON TS.ID = IT.TIPO_SERVICO_ID
          LEFT JOIN MULTAS M         ON M.ID = IT.MULTA_ID
          LEFT JOIN VEICULOS V       ON V.ID = M.VEICULO_ID
          LEFT JOIN PROCESSOS_CNH P  ON P.ID = IT.PROCESSO_CNH_ID
         WHERE CL.ARQUIVADO = 'N'
         ORDER BY CT.DATA_CONTRATACAO DESC, IT.ID DESC
        """;

    private const string SqlFases = """
        SELECT I.ID, I.ITEM_ID, I.FASE, I.SITUACAO, I.PRAZO_PROTOCOLO, I.DATA_PROTOCOLO,
               I.NUMERO_PROTOCOLO, I.ORGAO_DESTINATARIO, I.DATA_RESULTADO, I.RESULTADO,
               I.OBSERVACOES
          FROM INSTANCIAS I
          JOIN ITENS_CONTRATACAO IT ON IT.ID = I.ITEM_ID
          JOIN CONTRATACOES CT      ON CT.ID = IT.CONTRATACAO_ID
          JOIN CLIENTES CL          ON CL.ID = CT.CLIENTE_ID
         WHERE CL.ARQUIVADO = 'N'
         ORDER BY I.ITEM_ID, I.ID
        """;

    private const string SqlEtapas = """
        SELECT E.ID, E.ITEM_ID, EM.ORDEM, EM.NOME, E.CONCLUIDA, E.DATA_CONCLUSAO, E.PRAZO
          FROM ETAPAS E
          JOIN ETAPAS_MODELO EM     ON EM.ID = E.ETAPA_MODELO_ID
          JOIN ITENS_CONTRATACAO IT ON IT.ID = E.ITEM_ID
          JOIN CONTRATACOES CT      ON CT.ID = IT.CONTRATACAO_ID
          JOIN CLIENTES CL          ON CL.ID = CT.CLIENTE_ID
         WHERE CL.ARQUIVADO = 'N'
         ORDER BY E.ITEM_ID, EM.ORDEM
        """;

    /// <summary>Todos os processos do escritório, com fases e etapas, numa conexão só.</summary>
    /// <remarks>
    /// Traz tudo — inclusive o que já acabou —, e quem filtra é a tela, como no painel do
    /// dia. São três idas ao banco para a tela inteira; a alternativa, buscar as fases de
    /// cada processo sob demanda, seria uma ida por linha aberta.
    ///
    /// Isso vale enquanto a conta for de centenas por ano. Se um dia a lista pesar, o
    /// corte natural é parar de trazer os encerrados e buscá-los só quando a aba pedir —
    /// são eles que crescem para sempre, enquanto os em andamento ficam em dezenas.
    /// </remarks>
    public async Task<List<Processo>> ListarAsync(CancellationToken cancelamento = default)
    {
        await using var conexao = await Database.AbrirAsync(cancelamento);

        var processos = new List<Processo>();
        await using (var cmd = conexao.Comando(SqlProcessos))
        {
            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                processos.Add(new Processo
                {
                    Servico = new ServicoDaFicha
                    {
                        ItemId = reader.Inteiro(0),
                        ContratacaoId = reader.Inteiro(1),
                        Situacao = reader.Texto(2),
                        Servico = reader.Texto(3),
                        Ait = reader.TextoOuNulo(4),
                        Enquadramento = reader.TextoOuNulo(5),
                        Gravidade = reader.TextoOuNulo(6),
                        OrgaoAutuador = reader.TextoOuNulo(7),
                        Placa = reader.TextoOuNulo(8),
                        NumeroProcessoCnh = reader.TextoOuNulo(9),
                        DataContratacao = reader.Data(10),
                        ValorContratacao = reader.Decimal(11),
                        ItensNaContratacao = reader.Inteiro(12),
                        TipoProcessoCnh = reader.TextoOuNulo(13),
                        MultaId = reader.InteiroOuNulo(14),
                        ProcessoCnhId = reader.InteiroOuNulo(15)
                    },
                    ClienteId = reader.Inteiro(16),
                    ClienteNome = reader.Texto(17),
                    ClienteCpf = reader.Texto(18),
                    ClienteWhatsapp = reader.Texto(19)
                });
            }
        }

        if (processos.Count == 0)
        {
            return processos;
        }

        var porItem = processos.ToDictionary(p => p.Servico.ItemId, p => p.Servico);

        await using (var cmd = conexao.Comando(SqlFases))
        {
            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                var itemId = reader.Inteiro(1);
                if (!porItem.TryGetValue(itemId, out var servico))
                {
                    continue;
                }

                servico.Instancias.Add(new Instancia
                {
                    Id = reader.Inteiro(0),
                    ItemId = itemId,
                    Fase = ValorBanco.De<FaseInstancia>(reader.Texto(2)),
                    Situacao = ValorBanco.De<SituacaoInstancia>(reader.Texto(3)),
                    PrazoProtocolo = reader.DataOuNulo(4),
                    DataProtocolo = reader.DataOuNulo(5),
                    NumeroProtocolo = reader.TextoOuNulo(6),
                    OrgaoDestinatario = reader.TextoOuNulo(7),
                    DataResultado = reader.DataOuNulo(8),
                    Resultado = ValorBanco.DeOuNulo<ResultadoInstancia>(reader.TextoOuNulo(9)),
                    Observacoes = reader.TextoOuNulo(10)
                });
            }
        }

        await using (var cmd = conexao.Comando(SqlEtapas))
        {
            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                var itemId = reader.Inteiro(1);
                if (!porItem.TryGetValue(itemId, out var servico))
                {
                    continue;
                }

                servico.Etapas.Add(new EtapaDoServico
                {
                    Id = reader.Inteiro(0),
                    ItemId = itemId,
                    Ordem = reader.Inteiro(2),
                    Nome = reader.Texto(3),
                    Concluida = reader.Booleano(4),
                    DataConclusao = reader.DataOuNulo(5),
                    Prazo = reader.DataOuNulo(6)
                });
            }
        }

        return processos;
    }
}
