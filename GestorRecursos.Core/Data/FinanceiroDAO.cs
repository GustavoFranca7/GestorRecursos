using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: o dinheiro visto pelo escritorio, e nao por cliente.
//
// O PagamentoDAO continua dono do lancamento — registrar, excluir, listar de um cliente.
// Aqui ficam as duas perguntas que atravessam todo mundo: o caixa do mes e quem deve.
public class FinanceiroDAO
{
    // Entradas e saidas do mes na mesma consulta, para o extrato sair ordenado pelo
    // banco. Sao tabelas diferentes, mas a pergunta e uma so: o que passou pelo caixa.
    //
    // DATA_PAGAMENTO e DATA_DESPESA sao DATE e podem trazer hora, entao o corte usa
    // >= inicio e < inicio do mes seguinte. Com BETWEEN ate o ultimo dia, um lancamento
    // feito as 14h do dia 30 ficaria de fora.
    private const string SqlMovimentos = """
        SELECT 'E' AS TIPO, P.ID, P.DATA_PAGAMENTO AS QUANDO, P.VALOR,
               P.FORMA, P.PARCELAS, P.OBSERVACAO AS DESCRICAO,
               C.ID AS CONTRATACAO_ID, CL.ID AS CLIENTE_ID, CL.NOME
          FROM PAGAMENTOS P
          JOIN CONTRATACOES C ON C.ID = P.CONTRATACAO_ID
          JOIN CLIENTES CL    ON CL.ID = C.CLIENTE_ID
         WHERE P.DATA_PAGAMENTO >= :inicio AND P.DATA_PAGAMENTO < :fim
        UNION ALL
        SELECT 'S' AS TIPO, D.ID, D.DATA_DESPESA AS QUANDO, D.VALOR,
               NULL AS FORMA, 1 AS PARCELAS, D.TIPO AS DESCRICAO,
               C.ID AS CONTRATACAO_ID, CL.ID AS CLIENTE_ID, CL.NOME
          FROM DESPESAS D
          JOIN CONTRATACOES C ON C.ID = D.CONTRATACAO_ID
          JOIN CLIENTES CL    ON CL.ID = C.CLIENTE_ID
         WHERE D.DATA_DESPESA >= :inicio AND D.DATA_DESPESA < :fim
         ORDER BY QUANDO DESC, ID DESC
        """;

    // O que foi fechado no mes, que nao e o mesmo que o que entrou nele.
    private const string SqlContratado = """
        SELECT COUNT(*), NVL(SUM(VALOR_FINAL), 0)
          FROM CONTRATACOES
         WHERE DATA_CONTRATACAO >= :inicio AND DATA_CONTRATACAO < :fim
           AND SITUACAO <> 'CANCELADO'
        """;

    // A view ja sabe quem esta devendo; aqui se junta o historico de pagamentos e os
    // servicos da contratacao, que e o que a cobranca precisa dizer.
    private const string SqlCobrancas = """
        SELECT V.CONTRATACAO_ID, V.CLIENTE_ID, V.CLIENTE_NOME, CL.CPF, V.CLIENTE_WHATSAPP,
               V.DATA_CONTRATACAO, V.VALOR_FINAL, V.TOTAL_PAGO, V.EM_ABERTO,
               V.DIAS_DESDE_CONTRATACAO,
               (SELECT COUNT(*) FROM PAGAMENTOS P WHERE P.CONTRATACAO_ID = V.CONTRATACAO_ID),
               (SELECT MAX(P.DATA_PAGAMENTO) FROM PAGAMENTOS P WHERE P.CONTRATACAO_ID = V.CONTRATACAO_ID),
               (SELECT LISTAGG(TS.NOME, ', ') WITHIN GROUP (ORDER BY TS.NOME)
                  FROM ITENS_CONTRATACAO IT
                  JOIN TIPOS_SERVICO TS ON TS.ID = IT.TIPO_SERVICO_ID
                 WHERE IT.CONTRATACAO_ID = V.CONTRATACAO_ID)
          FROM VW_COBRANCAS_ABERTAS V
          JOIN CLIENTES CL ON CL.ID = V.CLIENTE_ID
         WHERE CL.ARQUIVADO = 'N'
         ORDER BY V.DIAS_DESDE_CONTRATACAO DESC, V.EM_ABERTO DESC
        """;

    /// <summary>O caixa de um mês: extrato, totais e o que foi fechado no período.</summary>
    public async Task<FechamentoDoMes> FecharMesAsync(
        int ano, int mes, CancellationToken cancelamento = default)
    {
        var inicio = new DateTime(ano, mes, 1);
        var fim = inicio.AddMonths(1);

        await using var conexao = await Database.AbrirAsync(cancelamento);

        var movimentos = new List<MovimentoDoCaixa>();
        await using (var cmd = conexao.Comando(SqlMovimentos))
        {
            cmd.Param("inicio", inicio);
            cmd.Param("fim", fim);

            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                movimentos.Add(new MovimentoDoCaixa
                {
                    Tipo = reader.Texto(0) == "E" ? TipoDeMovimento.Entrada : TipoDeMovimento.Saida,
                    Id = reader.Inteiro(1),
                    Quando = reader.Data(2),
                    Valor = reader.Decimal(3),
                    Forma = ValorBanco.DeOuNulo<FormaPagamento>(reader.TextoOuNulo(4)),
                    Parcelas = reader.Inteiro(5),
                    Descricao = reader.TextoOuNulo(6),
                    ContratacaoId = reader.Inteiro(7),
                    ClienteId = reader.Inteiro(8),
                    ClienteNome = reader.Texto(9)
                });
            }
        }

        var fechamento = new FechamentoDoMes
        {
            Ano = ano,
            Mes = mes,
            Movimentos = movimentos
        };

        await using (var cmd = conexao.Comando(SqlContratado))
        {
            cmd.Param("inicio", inicio);
            cmd.Param("fim", fim);

            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            if (await reader.ReadAsync(cancelamento))
            {
                fechamento.ContratacoesNoMes = reader.Inteiro(0);
                fechamento.ValorContratadoNoMes = reader.Decimal(1);
            }
        }

        return fechamento;
    }

    /// <summary>Tudo que está em aberto, do mais antigo para o mais recente.</summary>
    public async Task<List<CobrancaAberta>> ListarCobrancasAsync(
        CancellationToken cancelamento = default)
    {
        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(SqlCobrancas);
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);

        var cobrancas = new List<CobrancaAberta>();
        while (await reader.ReadAsync(cancelamento))
        {
            cobrancas.Add(new CobrancaAberta
            {
                ContratacaoId = reader.Inteiro(0),
                ClienteId = reader.Inteiro(1),
                ClienteNome = reader.Texto(2),
                ClienteCpf = reader.Texto(3),
                ClienteWhatsapp = reader.Texto(4),
                DataContratacao = reader.Data(5),
                ValorFinal = reader.Decimal(6),
                TotalPago = reader.Decimal(7),
                EmAberto = reader.Decimal(8),
                Dias = reader.Inteiro(9),
                QuantosPagamentos = reader.Inteiro(10),
                UltimoPagamento = reader.DataOuNulo(11),
                Servicos = reader.TextoOuNulo(12)
            });
        }

        return cobrancas;
    }
}
