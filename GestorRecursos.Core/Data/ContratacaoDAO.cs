using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: a contratação, seus itens e as instâncias que nascem com eles.
public class ContratacaoDAO
{
    /// <summary>Grava a contratação inteira: cabeçalho, itens, instâncias e, se houver, o pagamento.</summary>
    /// <remarks>
    /// Tudo numa transação só. Uma contratação com metade dos itens gravados é
    /// pior do que nenhuma: o valor fechado não bate com o que está lá dentro, e
    /// ninguém percebe até a hora de cobrar.
    ///
    /// O pagamento entra aqui, e não numa chamada depois, porque boa parte das
    /// contratações nasce paga no balcão: cobrar e registrar são o mesmo ato.
    /// </remarks>
    public async Task<int> CadastrarAsync(
        Contratacao contratacao,
        Pagamento? pagamento = null,
        ProcessoCnh? processoCnh = null,
        CancellationToken cancelamento = default)
    {
        const string sqlCabecalho = """
            INSERT INTO CONTRATACOES
                (CLIENTE_ID, DATA_CONTRATACAO, VALOR_TABELA, DESCONTO, VALOR_FINAL, SITUACAO, OBSERVACOES)
            VALUES
                (:clienteId, :dataContratacao, :valorTabela, :desconto, :valorFinal, :situacao, :observacoes)
            RETURNING ID INTO :novoId
            """;

        const string sqlItem = """
            INSERT INTO ITENS_CONTRATACAO
                (CONTRATACAO_ID, TIPO_SERVICO_ID, MULTA_ID, PROCESSO_CNH_ID, SITUACAO)
            VALUES
                (:contratacaoId, :tipoServicoId, :multaId, :processoCnhId, :situacao)
            RETURNING ID INTO :novoId
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var transacao = (OracleTransaction)await conexao.BeginTransactionAsync(cancelamento);

        try
        {
            // O processo do DETRAN entra antes dos itens: são eles que o apontam.
            if (processoCnh is not null)
            {
                processoCnh.ClienteId = contratacao.ClienteId;
                var idProcesso = await ProcessoCnhDAO.InserirAsync(
                    conexao, transacao, processoCnh, cancelamento);

                foreach (var item in contratacao.Itens)
                {
                    item.ProcessoCnhId = idProcesso;
                }
            }

            int idContratacao;
            await using (var cmd = conexao.Comando(sqlCabecalho, transacao))
            {
                cmd.Param("clienteId", contratacao.ClienteId)
                   .Param("dataContratacao", contratacao.DataContratacao == default ? DateTime.Today : contratacao.DataContratacao)
                   .Param("valorTabela", contratacao.ValorTabela)
                   .Param("desconto", contratacao.Desconto)
                   .Param("valorFinal", contratacao.ValorFinal)
                   .Param("situacao", contratacao.Situacao.Texto())
                   .Param("observacoes", contratacao.Observacoes);

                var pId = new OracleParameter("novoId", OracleDbType.Int32)
                {
                    Direction = System.Data.ParameterDirection.Output
                };
                cmd.Parameters.Add(pId);

                await cmd.ExecuteNonQueryAsync(cancelamento);
                idContratacao = Convert.ToInt32(pId.Value.ToString());
            }

            foreach (var item in contratacao.Itens)
            {
                int idItem;
                await using (var cmd = conexao.Comando(sqlItem, transacao))
                {
                    cmd.Param("contratacaoId", idContratacao)
                       .Param("tipoServicoId", item.TipoServicoId)
                       .Param("multaId", item.MultaId)
                       .Param("processoCnhId", item.ProcessoCnhId)
                       .Param("situacao", item.Situacao.Texto());

                    var pId = new OracleParameter("novoId", OracleDbType.Int32)
                    {
                        Direction = System.Data.ParameterDirection.Output
                    };
                    cmd.Parameters.Add(pId);

                    await cmd.ExecuteNonQueryAsync(cancelamento);
                    idItem = Convert.ToInt32(pId.Value.ToString());
                }

                item.Id = idItem;
                item.ContratacaoId = idContratacao;

                foreach (var instancia in item.Instancias)
                {
                    instancia.ItemId = idItem;
                    await InstanciaDAO.InserirAsync(conexao, transacao, instancia, cancelamento);
                }

                // Serviço direto nasce com o roteiro dele. Quem não tem modelo de etapa
                // não ganha nada: o INSERT ... SELECT simplesmente não acha linhas.
                await EtapaDAO.CriarDoModeloAsync(
                    conexao, transacao, idItem, item.TipoServicoId, cancelamento);
            }

            if (pagamento is not null)
            {
                pagamento.ContratacaoId = idContratacao;
                await PagamentoDAO.InserirAsync(conexao, transacao, pagamento, cancelamento);
            }

            await transacao.CommitAsync(cancelamento);
            contratacao.Id = idContratacao;
            return idContratacao;
        }
        catch
        {
            await transacao.RollbackAsync(cancelamento);
            throw;
        }
    }

    /// <summary>Contratação com o financeiro somado, lido da view.</summary>
    public async Task<Contratacao?> BuscarPorIdAsync(int id, CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT CONTRATACAO_ID, CLIENTE_ID, CLIENTE_NOME, CLIENTE_CPF,
                   DATA_CONTRATACAO, SITUACAO, VALOR_TABELA, DESCONTO, VALOR_FINAL,
                   TOTAL_PAGO, TOTAL_DESPESAS
              FROM VW_CONTRATACAO_FINANCEIRO
             WHERE CONTRATACAO_ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);

        Contratacao? contratacao = null;
        await using (var cmd = conexao.Comando(sql))
        {
            cmd.Param("id", id);
            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            if (await reader.ReadAsync(cancelamento))
            {
                contratacao = LerDaView(reader);
            }
        }

        if (contratacao is not null)
        {
            contratacao.Itens = await ListarItensAsync(conexao, id, cancelamento);
        }

        return contratacao;
    }

    /// <summary>Contratações de um cliente, com o financeiro de cada uma.</summary>
    public async Task<List<Contratacao>> ListarPorClienteAsync(int clienteId, CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT CONTRATACAO_ID, CLIENTE_ID, CLIENTE_NOME, CLIENTE_CPF,
                   DATA_CONTRATACAO, SITUACAO, VALOR_TABELA, DESCONTO, VALOR_FINAL,
                   TOTAL_PAGO, TOTAL_DESPESAS
              FROM VW_CONTRATACAO_FINANCEIRO
             WHERE CLIENTE_ID = :clienteId
             ORDER BY DATA_CONTRATACAO DESC, CONTRATACAO_ID DESC
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("clienteId", clienteId);

        var lista = new List<Contratacao>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(LerDaView(reader));
        }

        return lista;
    }

    /// <summary>Quem está devendo. É a primeira coisa que ela olha de manhã.</summary>
    public async Task<List<Contratacao>> ListarCobrancasAbertasAsync(CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT CONTRATACAO_ID, CLIENTE_ID, CLIENTE_NOME, CLIENTE_WHATSAPP,
                   DATA_CONTRATACAO, VALOR_FINAL, TOTAL_PAGO, EM_ABERTO, DIAS_DESDE_CONTRATACAO
              FROM VW_COBRANCAS_ABERTAS
             ORDER BY DIAS_DESDE_CONTRATACAO DESC
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);

        var lista = new List<Contratacao>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(new Contratacao
            {
                Id = reader.Inteiro(0),
                ClienteId = reader.Inteiro(1),
                ClienteNome = reader.Texto(2),
                DataContratacao = reader.Data(4),
                ValorFinal = reader.Decimal(5),
                TotalPago = reader.Decimal(6)
            });
        }

        return lista;
    }

    /// <summary>Corrige um serviço já contratado: a variante e as observações.</summary>
    /// <remarks>
    /// Existe porque dado digitado errado é a regra, não a exceção: escolheu "recurso de
    /// multa grave" e era média. Sem conserto, a saída seria cancelar e refazer, o que
    /// sujaria o histórico por causa de um engano de digitação.
    ///
    /// O valor cobrado não se mexe aqui, de propósito: dinheiro é assunto do Financeiro.
    /// O que acompanha a troca é só o valor de tabela — o "antes do desconto" —, porque
    /// senão a contratação ficaria dizendo que a soma dos serviços é outra.
    /// </remarks>
    public async Task<bool> CorrigirItemAsync(
        int itemId,
        int tipoServicoId,
        string? observacoes,
        CancellationToken cancelamento = default)
    {
        const string sqlItem = """
            UPDATE ITENS_CONTRATACAO SET TIPO_SERVICO_ID = :tipoServicoId WHERE ID = :itemId
            """;

        const string sqlContratacao = """
            UPDATE CONTRATACOES CT SET
                OBSERVACOES  = :observacoes,
                VALOR_TABELA = (SELECT NVL(SUM(T.VALOR_TABELA), 0)
                                  FROM ITENS_CONTRATACAO I
                                  JOIN TIPOS_SERVICO T ON T.ID = I.TIPO_SERVICO_ID
                                 WHERE I.CONTRATACAO_ID = CT.ID),
                DESCONTO     = GREATEST(0, (SELECT NVL(SUM(T.VALOR_TABELA), 0)
                                              FROM ITENS_CONTRATACAO I
                                              JOIN TIPOS_SERVICO T ON T.ID = I.TIPO_SERVICO_ID
                                             WHERE I.CONTRATACAO_ID = CT.ID) - CT.VALOR_FINAL)
             WHERE CT.ID = (SELECT CONTRATACAO_ID FROM ITENS_CONTRATACAO WHERE ID = :itemId)
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var transacao = (OracleTransaction)await conexao.BeginTransactionAsync(cancelamento);

        try
        {
            int afetadas;
            await using (var cmd = conexao.Comando(sqlItem, transacao))
            {
                cmd.Param("tipoServicoId", tipoServicoId).Param("itemId", itemId);
                afetadas = await cmd.ExecuteNonQueryAsync(cancelamento);
            }

            await using (var cmd = conexao.Comando(sqlContratacao, transacao))
            {
                cmd.Param("observacoes", observacoes).Param("itemId", itemId);
                await cmd.ExecuteNonQueryAsync(cancelamento);
            }

            await transacao.CommitAsync(cancelamento);
            return afetadas > 0;
        }
        catch
        {
            await transacao.RollbackAsync(cancelamento);
            throw;
        }
    }

    public async Task<bool> AtualizarValoresAsync(Contratacao contratacao, CancellationToken cancelamento = default)
    {
        const string sql = """
            UPDATE CONTRATACOES SET
                VALOR_TABELA = :valorTabela, DESCONTO = :desconto,
                VALOR_FINAL = :valorFinal, SITUACAO = :situacao, OBSERVACOES = :observacoes
             WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("valorTabela", contratacao.ValorTabela)
           .Param("desconto", contratacao.Desconto)
           .Param("valorFinal", contratacao.ValorFinal)
           .Param("situacao", contratacao.Situacao.Texto())
           .Param("observacoes", contratacao.Observacoes)
           .Param("id", contratacao.Id);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    // ---------------------------------------------------------------------

    private static async Task<List<ItemContratacao>> ListarItensAsync(
        OracleConnection conexao,
        int contratacaoId,
        CancellationToken cancelamento)
    {
        const string sql = """
            SELECT I.ID, I.CONTRATACAO_ID, I.TIPO_SERVICO_ID, I.MULTA_ID,
                   I.PROCESSO_CNH_ID, I.SITUACAO, T.NOME, T.VALOR_TABELA,
                   M.AIT, P.NUMERO_PROCESSO
              FROM ITENS_CONTRATACAO I
              JOIN TIPOS_SERVICO T ON T.ID = I.TIPO_SERVICO_ID
              LEFT JOIN MULTAS M ON M.ID = I.MULTA_ID
              LEFT JOIN PROCESSOS_CNH P ON P.ID = I.PROCESSO_CNH_ID
             WHERE I.CONTRATACAO_ID = :contratacaoId
             ORDER BY I.ID
            """;

        var itens = new List<ItemContratacao>();

        await using (var cmd = conexao.Comando(sql))
        {
            cmd.Param("contratacaoId", contratacaoId);

            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                itens.Add(new ItemContratacao
                {
                    Id = reader.Inteiro(0),
                    ContratacaoId = reader.Inteiro(1),
                    TipoServicoId = reader.Inteiro(2),
                    MultaId = reader.InteiroOuNulo(3),
                    ProcessoCnhId = reader.InteiroOuNulo(4),
                    Situacao = ValorBanco.De<SituacaoRegistro>(reader.Texto(5)),
                    TipoServicoNome = reader.Texto(6),
                    ValorTabela = reader.Decimal(7)
                });
            }
        }

        // As instâncias de todos os itens numa consulta só, e não uma por item:
        // uma contratação de 20 multas faria 20 idas à rede.
        if (itens.Count > 0)
        {
            var instancias = await InstanciaDAO.ListarPorContratacaoAsync(conexao, contratacaoId, cancelamento);
            foreach (var item in itens)
            {
                item.Instancias = instancias.Where(i => i.ItemId == item.Id).ToList();
            }
        }

        return itens;
    }

    private static Contratacao LerDaView(OracleDataReader reader) => new()
    {
        Id = reader.Inteiro(0),
        ClienteId = reader.Inteiro(1),
        ClienteNome = reader.Texto(2),
        ClienteCpf = reader.Texto(3),
        DataContratacao = reader.Data(4),
        Situacao = ValorBanco.De<SituacaoRegistro>(reader.Texto(5)),
        ValorTabela = reader.Decimal(6),
        Desconto = reader.Decimal(7),
        ValorFinal = reader.Decimal(8),
        TotalPago = reader.Decimal(9),
        TotalDespesas = reader.Decimal(10)
    };
}
