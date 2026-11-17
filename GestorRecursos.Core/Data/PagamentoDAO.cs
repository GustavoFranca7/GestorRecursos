using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: entradas de dinheiro, despesas e créditos.
public class PagamentoDAO
{
    public async Task<int> RegistrarAsync(Pagamento pagamento, CancellationToken cancelamento = default)
    {
        await using var conexao = await Database.AbrirAsync(cancelamento);
        return await InserirAsync(conexao, null, pagamento, cancelamento);
    }

    /// <summary>Insere o pagamento numa conexão que já está aberta, dentro da transação de quem chamou.</summary>
    /// <remarks>
    /// Existe para a contratação poder nascer paga de uma vez só: a entrada do dinheiro e
    /// o contrato que ela quita precisam cair juntos ou não cair.
    /// </remarks>
    internal static async Task<int> InserirAsync(
        OracleConnection conexao,
        OracleTransaction? transacao,
        Pagamento pagamento,
        CancellationToken cancelamento = default)
    {
        const string sql = """
            INSERT INTO PAGAMENTOS (CONTRATACAO_ID, DATA_PAGAMENTO, VALOR, FORMA, PARCELAS, OBSERVACAO)
            VALUES (:contratacaoId, :dataPagamento, :valor, :forma, :parcelas, :observacao)
            RETURNING ID INTO :novoId
            """;

        await using var cmd = conexao.Comando(sql, transacao);
        cmd.Param("contratacaoId", pagamento.ContratacaoId)
           .Param("dataPagamento", pagamento.DataPagamento == default ? DateTime.Today : pagamento.DataPagamento)
           .Param("valor", pagamento.Valor)
           .Param("forma", pagamento.Forma.Texto())
           .Param("parcelas", pagamento.Parcelas)
           .Param("observacao", pagamento.Observacao);

        var pId = new OracleParameter("novoId", OracleDbType.Int32)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        cmd.Parameters.Add(pId);

        await cmd.ExecuteNonQueryAsync(cancelamento);
        pagamento.Id = Convert.ToInt32(pId.Value.ToString());
        return pagamento.Id;
    }

    public async Task<List<Pagamento>> ListarPorContratacaoAsync(int contratacaoId, CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT ID, CONTRATACAO_ID, DATA_PAGAMENTO, VALOR, FORMA, PARCELAS, OBSERVACAO
              FROM PAGAMENTOS
             WHERE CONTRATACAO_ID = :contratacaoId
             ORDER BY DATA_PAGAMENTO, ID
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("contratacaoId", contratacaoId);

        var lista = new List<Pagamento>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(new Pagamento
            {
                Id = reader.Inteiro(0),
                ContratacaoId = reader.Inteiro(1),
                DataPagamento = reader.Data(2),
                Valor = reader.Decimal(3),
                Forma = ValorBanco.De<FormaPagamento>(reader.Texto(4)),
                Parcelas = reader.Inteiro(5),
                Observacao = reader.TextoOuNulo(6)
            });
        }

        return lista;
    }

    /// <summary>Todos os pagamentos do cliente, de todas as contratações dele.</summary>
    /// <remarks>
    /// A aba Financeiro mostra as contratações lado a lado; buscar os pagamentos de uma em
    /// uma seria uma ida ao banco por cartão. Quem chama agrupa por contratação.
    /// </remarks>
    public async Task<List<Pagamento>> ListarPorClienteAsync(int clienteId, CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT P.ID, P.CONTRATACAO_ID, P.DATA_PAGAMENTO, P.VALOR, P.FORMA,
                   P.PARCELAS, P.OBSERVACAO
              FROM PAGAMENTOS P
              JOIN CONTRATACOES C ON C.ID = P.CONTRATACAO_ID
             WHERE C.CLIENTE_ID = :clienteId
             ORDER BY P.DATA_PAGAMENTO, P.ID
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("clienteId", clienteId);

        var lista = new List<Pagamento>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(new Pagamento
            {
                Id = reader.Inteiro(0),
                ContratacaoId = reader.Inteiro(1),
                DataPagamento = reader.Data(2),
                Valor = reader.Decimal(3),
                Forma = ValorBanco.De<FormaPagamento>(reader.Texto(4)),
                Parcelas = reader.Inteiro(5),
                Observacao = reader.TextoOuNulo(6)
            });
        }

        return lista;
    }

    public async Task<bool> ExcluirAsync(int id, CancellationToken cancelamento = default)
    {
        // Pagamento lançado errado precisa poder sair, mas é o único registro
        // financeiro que se apaga de verdade: o resto se arquiva.
        const string sql = "DELETE FROM PAGAMENTOS WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", id);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    /// <summary>Quanto entrou no mês, para o painel.</summary>
    public async Task<decimal> TotalRecebidoNoMesAsync(int ano, int mes, CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT NVL(SUM(VALOR), 0)
              FROM PAGAMENTOS
             WHERE DATA_PAGAMENTO >= :inicio
               AND DATA_PAGAMENTO < ADD_MONTHS(:inicio, 1)
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("inicio", new DateTime(ano, mes, 1));

        return Convert.ToDecimal(await cmd.ExecuteScalarAsync(cancelamento));
    }

    // ---------------------------------------------------------------------
    // Despesas
    // ---------------------------------------------------------------------

    /// <summary>Insere a despesa na transação de quem chamou.</summary>
    /// <remarks>
    /// A contratação já nasce com as despesas combinadas na hora: a taxa que ela adianta
    /// entra no mesmo commit do contrato que a cobre, ou nenhum dos dois entra.
    /// </remarks>
    internal static async Task<int> InserirDespesaAsync(
        OracleConnection conexao,
        OracleTransaction? transacao,
        Despesa despesa,
        CancellationToken cancelamento = default)
    {
        const string sql = """
            INSERT INTO DESPESAS (CONTRATACAO_ID, TIPO, VALOR, DATA_DESPESA)
            VALUES (:contratacaoId, :tipo, :valor, :dataDespesa)
            RETURNING ID INTO :novoId
            """;

        await using var cmd = conexao.Comando(sql, transacao);
        cmd.Param("contratacaoId", despesa.ContratacaoId)
           .Param("tipo", despesa.Tipo)
           .Param("valor", despesa.Valor)
           .Param("dataDespesa", despesa.DataDespesa == default ? DateTime.Today : despesa.DataDespesa);

        var pId = new OracleParameter("novoId", OracleDbType.Int32)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        cmd.Parameters.Add(pId);

        await cmd.ExecuteNonQueryAsync(cancelamento);
        despesa.Id = Convert.ToInt32(pId.Value.ToString());
        return despesa.Id;
    }

    public async Task<int> RegistrarDespesaAsync(Despesa despesa, CancellationToken cancelamento = default)
    {
        const string sql = """
            INSERT INTO DESPESAS (CONTRATACAO_ID, TIPO, VALOR, DATA_DESPESA)
            VALUES (:contratacaoId, :tipo, :valor, :dataDespesa)
            RETURNING ID INTO :novoId
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("contratacaoId", despesa.ContratacaoId)
           .Param("tipo", despesa.Tipo)
           .Param("valor", despesa.Valor)
           .Param("dataDespesa", despesa.DataDespesa == default ? DateTime.Today : despesa.DataDespesa);

        var pId = new OracleParameter("novoId", OracleDbType.Int32)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        cmd.Parameters.Add(pId);

        await cmd.ExecuteNonQueryAsync(cancelamento);
        despesa.Id = Convert.ToInt32(pId.Value.ToString());
        return despesa.Id;
    }

    public async Task<List<Despesa>> ListarDespesasAsync(int contratacaoId, CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT ID, CONTRATACAO_ID, TIPO, VALOR, DATA_DESPESA
              FROM DESPESAS
             WHERE CONTRATACAO_ID = :contratacaoId
             ORDER BY DATA_DESPESA, ID
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("contratacaoId", contratacaoId);

        var lista = new List<Despesa>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(new Despesa
            {
                Id = reader.Inteiro(0),
                ContratacaoId = reader.Inteiro(1),
                Tipo = reader.Texto(2),
                Valor = reader.Decimal(3),
                DataDespesa = reader.Data(4)
            });
        }

        return lista;
    }

    /// <summary>Todas as despesas do cliente, pelo mesmo motivo dos pagamentos.</summary>
    public async Task<List<Despesa>> ListarDespesasPorClienteAsync(int clienteId, CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT D.ID, D.CONTRATACAO_ID, D.TIPO, D.VALOR, D.DATA_DESPESA
              FROM DESPESAS D
              JOIN CONTRATACOES C ON C.ID = D.CONTRATACAO_ID
             WHERE C.CLIENTE_ID = :clienteId
             ORDER BY D.DATA_DESPESA, D.ID
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("clienteId", clienteId);

        var lista = new List<Despesa>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(new Despesa
            {
                Id = reader.Inteiro(0),
                ContratacaoId = reader.Inteiro(1),
                Tipo = reader.Texto(2),
                Valor = reader.Decimal(3),
                DataDespesa = reader.Data(4)
            });
        }

        return lista;
    }

    // ---------------------------------------------------------------------
    // Créditos
    // ---------------------------------------------------------------------

    public async Task<int> RegistrarCreditoAsync(Credito credito, CancellationToken cancelamento = default)
    {
        const string sql = """
            INSERT INTO CREDITOS (CLIENTE_ID, VALOR, ORIGEM, UTILIZADO, DATA_CREDITO)
            VALUES (:clienteId, :valor, :origem, :utilizado, :dataCredito)
            RETURNING ID INTO :novoId
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("clienteId", credito.ClienteId)
           .Param("valor", credito.Valor)
           .Param("origem", credito.Origem)
           .Param("utilizado", credito.Utilizado.ParaSN())
           .Param("dataCredito", credito.DataCredito == default ? DateTime.Today : credito.DataCredito);

        var pId = new OracleParameter("novoId", OracleDbType.Int32)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        cmd.Parameters.Add(pId);

        await cmd.ExecuteNonQueryAsync(cancelamento);
        credito.Id = Convert.ToInt32(pId.Value.ToString());
        return credito.Id;
    }

    /// <summary>Quanto o cliente ainda tem a receber do escritório. Na tela, "saldo".</summary>
    public async Task<decimal> SaldoDisponivelAsync(
        int clienteId, CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT NVL(SUM(VALOR), 0)
              FROM CREDITOS
             WHERE CLIENTE_ID = :clienteId
               AND UTILIZADO = 'N'
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("clienteId", clienteId);

        var valor = await cmd.ExecuteScalarAsync(cancelamento);
        return valor is null or DBNull ? 0m : Convert.ToDecimal(valor);
    }

    /// <summary>Quita parte de uma contratação com o saldo que o cliente tem.</summary>
    /// <remarks>
    /// Vai em transação porque são duas coisas que precisam cair juntas: o pagamento, que
    /// é o que faz a dívida diminuir, e o consumo dos créditos que o bancaram. Metade
    /// disso gravado deixaria saldo gasto sem dívida abatida, ou o contrário.
    ///
    /// Os créditos são consumidos do mais antigo para o mais novo. Quando o último não
    /// cabe inteiro, ele é marcado como usado e o troco vira um crédito novo — em vez de
    /// uma coluna de "quanto já foi gasto deste aqui", que obrigaria a mexer na tabela e
    /// a reescrever toda consulta que hoje soma por UTILIZADO.
    ///
    /// Exige o <c>Database/13-PagamentoComSaldo.sql</c>: sem ele o INSERT bate no
    /// CK_PAGAMENTOS_FORMA e a transação inteira volta atrás.
    /// </remarks>
    public async Task<int> PagarComSaldoAsync(
        int contratacaoId,
        int clienteId,
        decimal valor,
        DateTime data,
        string? observacao = null,
        CancellationToken cancelamento = default)
    {
        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var transacao = (OracleTransaction)await conexao.BeginTransactionAsync(cancelamento);

        try
        {
            var id = await InserirAsync(conexao, transacao, new Pagamento
            {
                ContratacaoId = contratacaoId,
                DataPagamento = data,
                Valor = valor,
                Forma = FormaPagamento.Saldo,
                Parcelas = 1,
                Observacao = observacao
            }, cancelamento);

            await ConsumirSaldoAsync(conexao, transacao, clienteId, valor, data, cancelamento);

            await transacao.CommitAsync(cancelamento);
            return id;
        }
        catch
        {
            await transacao.RollbackAsync(cancelamento);
            throw;
        }
    }

    /// <summary>Gasta os créditos do cliente, do mais antigo para o mais novo.</summary>
    /// <remarks>
    /// Separado para a contratação poder nascer paga com saldo: lá o pagamento já entra
    /// na transação do contrato, e o que falta é só consumir o que o bancou.
    ///
    /// Quando o último crédito não cabe inteiro, ele é marcado como usado e o troco vira
    /// um crédito novo — em vez de uma coluna de "quanto já foi gasto deste aqui", que
    /// obrigaria a mexer na tabela e a reescrever toda consulta que soma por UTILIZADO.
    /// </remarks>
    internal static async Task ConsumirSaldoAsync(
        OracleConnection conexao,
        OracleTransaction? transacao,
        int clienteId,
        decimal valor,
        DateTime data,
        CancellationToken cancelamento = default)
    {
        const string sqlCreditos = """
            SELECT ID, VALOR
              FROM CREDITOS
             WHERE CLIENTE_ID = :clienteId
               AND UTILIZADO = 'N'
             ORDER BY DATA_CREDITO, ID
            """;

        const string sqlUsar = "UPDATE CREDITOS SET UTILIZADO = 'S' WHERE ID = :id";

        const string sqlTroco = """
            INSERT INTO CREDITOS (CLIENTE_ID, VALOR, ORIGEM, UTILIZADO, DATA_CREDITO)
            VALUES (:clienteId, :valor, :origem, 'N', :dataCredito)
            """;

        var disponiveis = new List<(int Id, decimal Valor)>();
        await using (var cmd = conexao.Comando(sqlCreditos, transacao))
        {
            cmd.Param("clienteId", clienteId);

            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                disponiveis.Add((reader.Inteiro(0), reader.Decimal(1)));
            }
        }

        var total = disponiveis.Sum(c => c.Valor);
        if (valor > total)
        {
            throw new InvalidOperationException(
                $"O saldo disponível é de {total:C}, menor que os {valor:C} pedidos.");
        }

        var falta = valor;
        foreach (var credito in disponiveis)
        {
            if (falta <= 0)
            {
                break;
            }

            await using (var cmd = conexao.Comando(sqlUsar, transacao))
            {
                cmd.Param("id", credito.Id);
                await cmd.ExecuteNonQueryAsync(cancelamento);
            }

            if (credito.Valor > falta)
            {
                await using var troco = conexao.Comando(sqlTroco, transacao);
                troco.Param("clienteId", clienteId)
                     .Param("valor", credito.Valor - falta)
                     .Param("origem", $"Resto do saldo usado em {data:dd/MM/yyyy}")
                     .Param("dataCredito", data);

                await troco.ExecuteNonQueryAsync(cancelamento);
            }

            falta -= credito.Valor;
        }
    }

    /// <summary>Marca o crédito como usado, ou devolve à disponibilidade.</summary>
    /// <remarks>
    /// Usar é sempre um gesto de fora do sistema — vira desconto na próxima negociação —,
    /// então o que fica guardado é só o fato de já ter sido usado. Dá para desmarcar
    /// porque "marquei o crédito errado" é um erro que acontece.
    /// </remarks>
    public async Task<bool> MarcarCreditoUsadoAsync(
        int id, bool usado, CancellationToken cancelamento = default)
    {
        const string sql = "UPDATE CREDITOS SET UTILIZADO = :utilizado WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("utilizado", usado.ParaSN())
           .Param("id", id);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    public async Task<List<Credito>> ListarCreditosAsync(int clienteId, bool apenasDisponiveis = true, CancellationToken cancelamento = default)
    {
        var filtro = apenasDisponiveis ? "AND UTILIZADO = 'N'" : string.Empty;
        var sql = $"""
            SELECT ID, CLIENTE_ID, VALOR, ORIGEM, UTILIZADO, DATA_CREDITO
              FROM CREDITOS
             WHERE CLIENTE_ID = :clienteId {filtro}
             ORDER BY DATA_CREDITO DESC
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("clienteId", clienteId);

        var lista = new List<Credito>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(new Credito
            {
                Id = reader.Inteiro(0),
                ClienteId = reader.Inteiro(1),
                Valor = reader.Decimal(2),
                Origem = reader.TextoOuNulo(3),
                Utilizado = reader.Booleano(4),
                DataCredito = reader.Data(5)
            });
        }

        return lista;
    }
}
