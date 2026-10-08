using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: a ficha do cliente.
//
// Fica fora do ClienteDAO porque atravessa cinco tabelas e nenhuma delas é CLIENTES:
// veículos, multas, contratações, itens e instâncias. O ClienteDAO continua sendo o
// dono do cadastro; aqui é a visão reunida que a tela da ficha precisa.
public class FichaClienteDAO
{
    /// <summary>Monta a ficha inteira numa conexão só.</summary>
    /// <remarks>
    /// São quatro consultas em sequência, e não quatro chamadas de fora: abrir conexão
    /// contra a nuvem é a parte cara, e abrir uma vez já paga por todas.
    /// </remarks>
    public async Task<FichaCliente?> BuscarAsync(int clienteId, CancellationToken cancelamento = default)
    {
        await using var conexao = await Database.AbrirAsync(cancelamento);

        var cliente = await LerClienteAsync(conexao, clienteId, cancelamento);
        if (cliente is null)
        {
            return null;
        }

        return new FichaCliente
        {
            Cliente = cliente,
            Veiculos = await LerVeiculosAsync(conexao, clienteId, cancelamento),
            Processos = await LerProcessosAsync(conexao, clienteId, cancelamento),
            Financeiro = await LerFinanceiroAsync(conexao, clienteId, cancelamento)
        };
    }

    private static async Task<Cliente?> LerClienteAsync(
        OracleConnection conexao, int clienteId, CancellationToken cancelamento)
    {
        const string sql = """
            SELECT C.ID, C.NOME, C.CPF, C.EMAIL, C.WHATSAPP,
                   C.CNH_NUMERO, C.CNH_CATEGORIA, C.CNH_VALIDADE, C.CNH_REGISTRO,
                   C.RG, C.RG_ORGAO, C.NACIONALIDADE, C.ESTADO_CIVIL, C.PROFISSAO,
                   C.OBSERVACOES, C.ARQUIVADO, C.DATA_CADASTRO,
                   E.ID, E.CEP, E.LOGRADOURO, E.NUMERO, E.COMPLEMENTO, E.BAIRRO, E.CIDADE, E.ESTADO
              FROM CLIENTES C
              LEFT JOIN ENDERECOS E ON E.CLIENTE_ID = C.ID
             WHERE C.ID = :id
            """;

        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", clienteId);

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        if (!await reader.ReadAsync(cancelamento))
        {
            return null;
        }

        var cliente = new Cliente
        {
            Id = reader.Inteiro(0),
            Nome = reader.Texto(1),
            Cpf = reader.Texto(2),
            Email = reader.TextoOuNulo(3),
            Whatsapp = reader.Texto(4),
            CnhNumero = reader.TextoOuNulo(5),
            CnhCategoria = reader.TextoOuNulo(6),
            CnhValidade = reader.DataOuNulo(7),
            CnhRegistro = reader.TextoOuNulo(8),
            Rg = reader.TextoOuNulo(9),
            RgOrgao = reader.TextoOuNulo(10),
            Nacionalidade = reader.TextoOuNulo(11),
            EstadoCivil = reader.TextoOuNulo(12),
            Profissao = reader.TextoOuNulo(13),
            Observacoes = reader.TextoOuNulo(14),
            Arquivado = reader.Booleano(15),
            DataCadastro = reader.Data(16)
        };

        if (!reader.IsDBNull(17))
        {
            cliente.Endereco = new Endereco
            {
                Id = reader.Inteiro(17),
                ClienteId = cliente.Id,
                Cep = reader.TextoOuNulo(18),
                Logradouro = reader.Texto(19),
                Numero = reader.TextoOuNulo(20),
                Complemento = reader.TextoOuNulo(21),
                Bairro = reader.TextoOuNulo(22),
                Cidade = reader.Texto(23),
                Estado = reader.Texto(24)
            };
        }

        return cliente;
    }

    private static async Task<List<VeiculoDaFicha>> LerVeiculosAsync(
        OracleConnection conexao, int clienteId, CancellationToken cancelamento)
    {
        const string sql = """
            SELECT V.ID, V.PLACA, V.MARCA_MODELO, V.COR, V.ANO, CV.VINCULO,
                   (SELECT COUNT(*) FROM MULTAS M WHERE M.VEICULO_ID = V.ID)
              FROM CLIENTE_VEICULO CV
              JOIN VEICULOS V ON V.ID = CV.VEICULO_ID
             WHERE CV.CLIENTE_ID = :id
             ORDER BY V.PLACA
            """;

        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", clienteId);

        var lista = new List<VeiculoDaFicha>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(new VeiculoDaFicha
            {
                Id = reader.Inteiro(0),
                Placa = reader.Texto(1),
                MarcaModelo = reader.TextoOuNulo(2),
                Cor = reader.TextoOuNulo(3),
                Ano = reader.TextoOuNulo(4),
                Vinculo = reader.Texto(5),
                QuantidadeMultas = reader.Inteiro(6)
            });
        }

        return lista;
    }

    /// <summary>Os recursos que ainda não terminaram, do prazo mais apertado para o mais folgado.</summary>
    private static async Task<List<ProcessoDaFicha>> LerProcessosAsync(
        OracleConnection conexao, int clienteId, CancellationToken cancelamento)
    {
        const string sql = """
            SELECT I.ID, TS.NOME, M.AIT, V.PLACA, M.ENQUADRAMENTO, M.GRAVIDADE,
                   I.FASE, I.SITUACAO, I.PRAZO_PROTOCOLO, I.DATA_PROTOCOLO
              FROM INSTANCIAS I
              JOIN ITENS_CONTRATACAO IT ON IT.ID = I.ITEM_ID
              JOIN CONTRATACOES CT      ON CT.ID = IT.CONTRATACAO_ID
              JOIN TIPOS_SERVICO TS     ON TS.ID = IT.TIPO_SERVICO_ID
              LEFT JOIN MULTAS M        ON M.ID = IT.MULTA_ID
              LEFT JOIN VEICULOS V      ON V.ID = M.VEICULO_ID
             WHERE CT.CLIENTE_ID = :id
               AND I.SITUACAO NOT IN ('CANCELADO','DESISTENCIA','DEFERIDO','INDEFERIDO')
             ORDER BY NVL(I.PRAZO_PROTOCOLO, DATE '9999-12-31'), I.ID
            """;

        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", clienteId);

        var lista = new List<ProcessoDaFicha>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(new ProcessoDaFicha
            {
                InstanciaId = reader.Inteiro(0),
                Servico = reader.Texto(1),
                Ait = reader.TextoOuNulo(2),
                Placa = reader.TextoOuNulo(3),
                Enquadramento = reader.TextoOuNulo(4),
                Gravidade = reader.TextoOuNulo(5),
                Fase = reader.Texto(6),
                Situacao = reader.Texto(7),
                PrazoProtocolo = reader.DataOuNulo(8),
                DataProtocolo = reader.DataOuNulo(9)
            });
        }

        return lista;
    }

    private static async Task<ResumoFinanceiroCliente> LerFinanceiroAsync(
        OracleConnection conexao, int clienteId, CancellationToken cancelamento)
    {
        // A visão já sabe somar pagamento e desconto por contratação; aqui só se junta
        // o que é do mesmo cliente. Contratação cancelada fica de fora da conta.
        //
        // A data do último pagamento vem numa segunda consulta, e não como subconsulta na
        // mesma lista: misturada com SUM() sem GROUP BY, o Oracle recusa com ORA-00937.
        const string sqlTotais = """
            SELECT NVL(SUM(F.VALOR_FINAL), 0), NVL(SUM(F.TOTAL_PAGO), 0)
              FROM VW_CONTRATACAO_FINANCEIRO F
             WHERE F.CLIENTE_ID = :id
               AND F.SITUACAO <> 'CANCELADO'
            """;

        const string sqlUltimoPagamento = """
            SELECT MAX(P.DATA_PAGAMENTO)
              FROM PAGAMENTOS P
              JOIN CONTRATACOES C ON C.ID = P.CONTRATACAO_ID
             WHERE C.CLIENTE_ID = :id
            """;

        var resumo = new ResumoFinanceiroCliente();

        await using (var cmd = conexao.Comando(sqlTotais))
        {
            cmd.Param("id", clienteId);

            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            if (await reader.ReadAsync(cancelamento))
            {
                resumo.TotalContratado = reader.Decimal(0);
                resumo.TotalPago = reader.Decimal(1);
            }
        }

        await using (var cmd = conexao.Comando(sqlUltimoPagamento))
        {
            cmd.Param("id", clienteId);

            var valor = await cmd.ExecuteScalarAsync(cancelamento);
            if (valor is DateTime data)
            {
                resumo.UltimoPagamento = data;
            }
        }

        return resumo;
    }
}
