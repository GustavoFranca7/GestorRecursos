using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: entradas de dinheiro, despesas e créditos.
public class PagamentoDAO
{
    public async Task<int> RegistrarAsync(Pagamento pagamento, CancellationToken cancelamento = default)
    {
        const string sql = """
            INSERT INTO PAGAMENTOS (CONTRATACAO_ID, DATA_PAGAMENTO, VALOR, FORMA, PARCELAS, OBSERVACAO)
            VALUES (:contratacaoId, :dataPagamento, :valor, :forma, :parcelas, :observacao)
            RETURNING ID INTO :novoId
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
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
