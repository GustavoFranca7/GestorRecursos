using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: acesso à tabela CLIENTES e ao endereço que pende dela.
public class ClienteDAO
{
    // Lista de colunas repetida em três consultas. Declarada uma vez para que o
    // mapeamento por índice (reader.Texto(1), (2)...) não saia do lugar quando
    // alguém acrescentar uma coluna.
    private const string ColunasCliente = """
        C.ID, C.NOME, C.CPF, C.EMAIL, C.WHATSAPP,
        C.CNH_NUMERO, C.CNH_CATEGORIA, C.CNH_VALIDADE, C.CNH_REGISTRO,
        C.RG, C.RG_ORGAO, C.NACIONALIDADE, C.ESTADO_CIVIL, C.PROFISSAO,
        C.OBSERVACOES, C.ARQUIVADO, C.DATA_CADASTRO
        """;

    private const string ColunasEndereco = """
        E.ID, E.CEP, E.LOGRADOURO, E.NUMERO, E.COMPLEMENTO, E.BAIRRO, E.CIDADE, E.ESTADO
        """;

    // Blocos comuns das consultas da lista. O prazo e o valor em aberto saem de
    // agregados por cliente: calculados antes, no WITH, para que um cliente com três
    // contratações e cinco instâncias continue valendo uma linha só na tela.
    private const string ResumoApoio = $"""
        WITH PRAZOS AS (
            SELECT CO.CLIENTE_ID,
                   MIN(I.PRAZO_PROTOCOLO) AS PRAZO,
                   MIN(I.FASE) KEEP (DENSE_RANK FIRST ORDER BY I.PRAZO_PROTOCOLO) AS FASE
              FROM INSTANCIAS I
              JOIN ITENS_CONTRATACAO IT ON IT.ID = I.ITEM_ID
              JOIN CONTRATACOES CO      ON CO.ID = IT.CONTRATACAO_ID
             WHERE I.DATA_PROTOCOLO IS NULL
               AND I.PRAZO_PROTOCOLO IS NOT NULL
               AND I.SITUACAO NOT IN {SituacoesSql.InstanciaEncerrada}
             GROUP BY CO.CLIENTE_ID
        ),
        ABERTO AS (
            SELECT CLIENTE_ID, SUM(EM_ABERTO) AS EM_ABERTO
              FROM VW_CONTRATACAO_FINANCEIRO
             WHERE EM_ABERTO > 0
               AND SITUACAO <> 'CANCELADO'
             GROUP BY CLIENTE_ID
        )
        """;

    private const string ResumoDe = """
          FROM CLIENTES C
          LEFT JOIN ENDERECOS E ON E.CLIENTE_ID = C.ID
          LEFT JOIN ABERTO    A ON A.CLIENTE_ID = C.ID
          LEFT JOIN PRAZOS    P ON P.CLIENTE_ID = C.ID
        """;

    /// <summary>Monta a condição de cada aba da lista.</summary>
    private static string CondicaoDoFiltro(FiltroClientes filtro) => filtro switch
    {
        FiltroClientes.Arquivados => "C.ARQUIVADO = 'S'",
        FiltroClientes.ComPendencia => "C.ARQUIVADO = 'N' AND A.EM_ABERTO > 0",
        FiltroClientes.ComPrazo => "C.ARQUIVADO = 'N' AND P.PRAZO IS NOT NULL AND P.PRAZO <= TRUNC(SYSDATE) + 7",
        _ => "C.ARQUIVADO = 'N'"
    };

    /// <summary>Lista uma página da tela de clientes, com placas, valor em aberto e prazo.</summary>
    /// <remarks>
    /// Consulta própria, e não a <see cref="ListarPaginaAsync"/> com mais colunas: a tela
    /// de lista precisa de agregados que o cadastro não usa, e o cadastro precisa de
    /// campos que a lista não mostra. Juntar as duas faria cada uma pagar pela outra.
    /// </remarks>
    public async Task<Pagina<ResumoCliente>> ListarResumoAsync(
        int numeroPagina = 1,
        int tamanhoPagina = 11,
        FiltroClientes filtro = FiltroClientes.Ativos,
        string? termo = null,
        CancellationToken cancelamento = default)
    {
        if (numeroPagina < 1) numeroPagina = 1;
        if (tamanhoPagina is < 1 or > 200) tamanhoPagina = 11;

        var condicao = CondicaoDoFiltro(filtro);

        // Filtro simples por nome ou CPF, para o campo "filtrar nesta lista" da ficha.
        // A busca global, essa sim, é outra história e mora no BuscaDAO.
        var procurando = !string.IsNullOrWhiteSpace(termo);
        if (procurando)
        {
            condicao += " AND (UPPER(C.NOME) LIKE '%' || :termo || '%' OR C.CPF LIKE '%' || :termo || '%')";
        }

        var sqlTotal = $"""
            {ResumoApoio}
            SELECT COUNT(*)
            {ResumoDe}
             WHERE {condicao}
            """;

        var sqlPagina = $"""
            {ResumoApoio}
            SELECT C.ID, C.NOME, C.CPF, C.WHATSAPP, C.ARQUIVADO,
                   E.CIDADE, E.ESTADO,
                   (SELECT LISTAGG(V.PLACA, ', ') WITHIN GROUP (ORDER BY V.PLACA)
                      FROM CLIENTE_VEICULO CV
                      JOIN VEICULOS V ON V.ID = CV.VEICULO_ID
                     WHERE CV.CLIENTE_ID = C.ID) AS PLACAS,
                   NVL(A.EM_ABERTO, 0) AS EM_ABERTO,
                   P.PRAZO, P.FASE
            {ResumoDe}
             WHERE {condicao}
             ORDER BY C.NOME
            OFFSET :pular ROWS FETCH NEXT :quantidade ROWS ONLY
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);

        var termoMaiusculo = termo?.Trim().ToUpperInvariant() ?? string.Empty;

        int total;
        await using (var cmdTotal = conexao.Comando(sqlTotal))
        {
            if (procurando) cmdTotal.Param("termo", termoMaiusculo);
            total = Convert.ToInt32(await cmdTotal.ExecuteScalarAsync(cancelamento));
        }

        var itens = new List<ResumoCliente>();
        await using (var cmd = conexao.Comando(sqlPagina))
        {
            cmd.Param("pular", (numeroPagina - 1) * tamanhoPagina)
               .Param("quantidade", tamanhoPagina);

            if (procurando) cmd.Param("termo", termoMaiusculo);

            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                itens.Add(new ResumoCliente
                {
                    Id = reader.Inteiro(0),
                    Nome = reader.Texto(1),
                    Cpf = reader.Texto(2),
                    Whatsapp = reader.Texto(3),
                    Arquivado = reader.Booleano(4),
                    Cidade = reader.TextoOuNulo(5),
                    Estado = reader.TextoOuNulo(6),
                    Placas = reader.TextoOuNulo(7),
                    EmAberto = reader.Decimal(8),
                    PrazoMaisProximo = reader.DataOuNulo(9),
                    FaseDoPrazo = reader.TextoOuNulo(10)
                });
            }
        }

        return new Pagina<ResumoCliente>
        {
            NumeroPagina = numeroPagina,
            TamanhoPagina = tamanhoPagina,
            TotalRegistros = total,
            Itens = itens
        };
    }

    /// <summary>O que vai junto se este cliente for apagado de vez.</summary>
    /// <remarks>
    /// Serve para a tela avisar antes, com número: apagar um cliente leva o histórico
    /// financeiro dele embora, e isso não pode ser descoberto depois do clique.
    /// </remarks>
    public async Task<ResumoExclusao?> ResumirExclusaoAsync(int id, CancellationToken cancelamento = default)
    {
        const string sql = """
            SELECT C.ARQUIVADO,
                   (SELECT COUNT(*) FROM CONTRATACOES  WHERE CLIENTE_ID = C.ID),
                   (SELECT COUNT(*) FROM PROCESSOS_CNH WHERE CLIENTE_ID = C.ID),
                   (SELECT COUNT(*) FROM PAGAMENTOS P
                      JOIN CONTRATACOES CT ON CT.ID = P.CONTRATACAO_ID
                     WHERE CT.CLIENTE_ID = C.ID),
                   (SELECT NVL(SUM(P.VALOR), 0) FROM PAGAMENTOS P
                      JOIN CONTRATACOES CT ON CT.ID = P.CONTRATACAO_ID
                     WHERE CT.CLIENTE_ID = C.ID)
              FROM CLIENTES C
             WHERE C.ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", id);

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        if (!await reader.ReadAsync(cancelamento))
        {
            return null;
        }

        return new ResumoExclusao
        {
            Arquivado = reader.Booleano(0),
            Contratacoes = reader.Inteiro(1),
            Processos = reader.Inteiro(2),
            Pagamentos = reader.Inteiro(3),
            TotalPago = reader.Decimal(4)
        };
    }

    /// <summary>Apaga o cliente do banco, de verdade. Só vale para quem já está arquivado.</summary>
    /// <remarks>
    /// Regra desta fase: ninguém apaga sem antes arquivar. A conferência é feita aqui, e não
    /// só na tela, porque a API vai chamar o mesmo método.
    ///
    /// A ordem abaixo existe porque nem toda chave estrangeira apaga em cascata: endereço,
    /// veículos do cliente, itens, instâncias e etapas vão sozinhos; pagamento, despesa,
    /// crédito, contratação, processo, arquivo e agenda precisam sair na mão, de dentro
    /// para fora. Tudo numa transação: ou o cliente some inteiro, ou não sai nada.
    /// </remarks>
    public async Task<bool> ExcluirDefinitivoAsync(int id, CancellationToken cancelamento = default)
    {
        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var transacao = (OracleTransaction)await conexao.BeginTransactionAsync(cancelamento);

        try
        {
            await using (var conferencia = conexao.Comando(
                "SELECT ARQUIVADO FROM CLIENTES WHERE ID = :id", transacao))
            {
                conferencia.Param("id", id);
                var arquivado = await conferencia.ExecuteScalarAsync(cancelamento) as string;

                if (arquivado is null)
                {
                    await transacao.RollbackAsync(cancelamento);
                    return false;
                }

                if (arquivado != "S")
                {
                    // Sem rollback aqui: quem lança cai no catch, que desfaz. Desfazer
                    // duas vezes dá ORA-50045 na transação já fechada, e esse erro
                    // substitui a mensagem acima por algo que ninguém entende.
                    throw new InvalidOperationException(
                        "Só é possível excluir um cliente que já esteja arquivado.");
                }
            }

            string[] passos =
            [
                "DELETE FROM ARQUIVOS WHERE CLIENTE_ID = :id",
                "DELETE FROM AGENDA   WHERE CLIENTE_ID = :id",
                """
                DELETE FROM PAGAMENTOS
                 WHERE CONTRATACAO_ID IN (SELECT ID FROM CONTRATACOES WHERE CLIENTE_ID = :id)
                """,
                """
                DELETE FROM DESPESAS
                 WHERE CONTRATACAO_ID IN (SELECT ID FROM CONTRATACOES WHERE CLIENTE_ID = :id)
                """,
                "DELETE FROM CREDITOS      WHERE CLIENTE_ID = :id",
                "DELETE FROM CONTRATACOES  WHERE CLIENTE_ID = :id",
                "DELETE FROM PROCESSOS_CNH WHERE CLIENTE_ID = :id"
            ];

            foreach (var passo in passos)
            {
                await using var cmd = conexao.Comando(passo, transacao);
                cmd.Param("id", id);
                await cmd.ExecuteNonQueryAsync(cancelamento);
            }

            int apagados;
            await using (var cmd = conexao.Comando("DELETE FROM CLIENTES WHERE ID = :id", transacao))
            {
                cmd.Param("id", id);
                apagados = await cmd.ExecuteNonQueryAsync(cancelamento);
            }

            await transacao.CommitAsync(cancelamento);
            return apagados > 0;
        }
        catch
        {
            await transacao.RollbackAsync(cancelamento);
            throw;
        }
    }

    /// <summary>Monta as linhas da lista para um punhado de clientes já identificados.</summary>
    /// <remarks>
    /// É o que liga a busca global à lista: a busca devolve de quem é a placa, o AIT ou o
    /// processo, e daqui saem as mesmas colunas da listagem normal. Sem paginação de
    /// propósito — a busca já vem limitada, e paginar resultado de busca confunde.
    /// </remarks>
    public async Task<List<ResumoCliente>> ListarResumoPorIdsAsync(
        IReadOnlyList<int> ids,
        bool arquivados = false,
        CancellationToken cancelamento = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        // Um parâmetro por id. Montar a lista direto no texto do SQL abriria a porta
        // para injeção, mesmo vindo de int.
        var nomes = string.Join(", ", ids.Select((_, i) => $":id{i}"));

        var sql = $"""
            {ResumoApoio}
            SELECT C.ID, C.NOME, C.CPF, C.WHATSAPP, C.ARQUIVADO,
                   E.CIDADE, E.ESTADO,
                   (SELECT LISTAGG(V.PLACA, ', ') WITHIN GROUP (ORDER BY V.PLACA)
                      FROM CLIENTE_VEICULO CV
                      JOIN VEICULOS V ON V.ID = CV.VEICULO_ID
                     WHERE CV.CLIENTE_ID = C.ID) AS PLACAS,
                   NVL(A.EM_ABERTO, 0) AS EM_ABERTO,
                   P.PRAZO, P.FASE
            {ResumoDe}
             WHERE C.ID IN ({nomes})
               AND C.ARQUIVADO = :arquivado
             ORDER BY C.NOME
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);

        // O arquivo e a listagem são lugares diferentes: quem busca em um não pode
        // receber resultado do outro, nem com a busca global encontrando os dois.
        cmd.Param("arquivado", arquivados.ParaSN());

        for (var i = 0; i < ids.Count; i++)
        {
            cmd.Param($"id{i}", ids[i]);
        }

        var itens = new List<ResumoCliente>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            itens.Add(new ResumoCliente
            {
                Id = reader.Inteiro(0),
                Nome = reader.Texto(1),
                Cpf = reader.Texto(2),
                Whatsapp = reader.Texto(3),
                Arquivado = reader.Booleano(4),
                Cidade = reader.TextoOuNulo(5),
                Estado = reader.TextoOuNulo(6),
                Placas = reader.TextoOuNulo(7),
                EmAberto = reader.Decimal(8),
                PrazoMaisProximo = reader.DataOuNulo(9),
                FaseDoPrazo = reader.TextoOuNulo(10)
            });
        }

        return itens;
    }

    /// <summary>Os três números do cabeçalho da lista, numa consulta só.</summary>
    public async Task<ContagemClientes> ContarAsync(CancellationToken cancelamento = default)
    {
        var sql = $"""
            {ResumoApoio}
            SELECT COUNT(*),
                   COUNT(CASE WHEN A.EM_ABERTO > 0 THEN 1 END),
                   COUNT(CASE WHEN P.PRAZO <= TRUNC(SYSDATE) + 7 THEN 1 END)
            {ResumoDe}
             WHERE C.ARQUIVADO = 'N'
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);

        if (!await reader.ReadAsync(cancelamento))
        {
            return new ContagemClientes();
        }

        return new ContagemClientes
        {
            Cadastrados = reader.Inteiro(0),
            ComValorEmAberto = reader.Inteiro(1),
            ComPrazoNaSemana = reader.Inteiro(2)
        };
    }

    /// <summary>Lista uma página de clientes, já com o endereço de cada um.</summary>
    /// <remarks>
    /// A paginação é resolvida no banco (OFFSET/FETCH) e o endereço vem no mesmo
    /// SELECT: sem isso, uma lista de 20 clientes viraria 21 idas à rede, e cada
    /// ida custa dezenas de milissegundos agora que o banco está na nuvem.
    /// </remarks>
    public async Task<Pagina<Cliente>> ListarPaginaAsync(
        int numeroPagina = 1,
        int tamanhoPagina = 20,
        bool incluirArquivados = false,
        CancellationToken cancelamento = default)
    {
        if (numeroPagina < 1) numeroPagina = 1;
        if (tamanhoPagina is < 1 or > 200) tamanhoPagina = 20;

        var filtro = incluirArquivados ? string.Empty : "WHERE C.ARQUIVADO = 'N'";

        var sqlTotal = $"SELECT COUNT(*) FROM CLIENTES C {filtro}";

        var sqlPagina = $"""
            SELECT {ColunasCliente}, {ColunasEndereco}
              FROM CLIENTES C
              LEFT JOIN ENDERECOS E ON E.CLIENTE_ID = C.ID
              {filtro}
             ORDER BY C.NOME
            OFFSET :pular ROWS FETCH NEXT :quantidade ROWS ONLY
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);

        int total;
        await using (var cmdTotal = conexao.Comando(sqlTotal))
        {
            total = Convert.ToInt32(await cmdTotal.ExecuteScalarAsync(cancelamento));
        }

        var itens = new List<Cliente>();
        await using (var cmd = conexao.Comando(sqlPagina))
        {
            cmd.Param("pular", (numeroPagina - 1) * tamanhoPagina)
               .Param("quantidade", tamanhoPagina);

            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                itens.Add(LerCliente(reader, colunaEndereco: 17));
            }
        }

        return new Pagina<Cliente>
        {
            NumeroPagina = numeroPagina,
            TamanhoPagina = tamanhoPagina,
            TotalRegistros = total,
            Itens = itens
        };
    }

    /// <summary>Busca um cliente pelo id, com endereço e veículos.</summary>
    public async Task<Cliente?> BuscarPorIdAsync(int id, CancellationToken cancelamento = default)
    {
        var sql = $"""
            SELECT {ColunasCliente}, {ColunasEndereco}
              FROM CLIENTES C
              LEFT JOIN ENDERECOS E ON E.CLIENTE_ID = C.ID
             WHERE C.ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);

        Cliente? cliente = null;
        await using (var cmd = conexao.Comando(sql))
        {
            cmd.Param("id", id);

            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            if (await reader.ReadAsync(cancelamento))
            {
                cliente = LerCliente(reader, colunaEndereco: 17);
            }
        }

        if (cliente is not null)
        {
            cliente.Veiculos = await ListarVeiculosAsync(conexao, cliente.Id, cancelamento);
        }

        return cliente;
    }

    public async Task<Cliente?> BuscarPorCpfAsync(string cpf, CancellationToken cancelamento = default)
    {
        var sql = $"""
            SELECT {ColunasCliente}, {ColunasEndereco}
              FROM CLIENTES C
              LEFT JOIN ENDERECOS E ON E.CLIENTE_ID = C.ID
             WHERE C.CPF = :cpf
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("cpf", cpf);

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        return await reader.ReadAsync(cancelamento) ? LerCliente(reader, colunaEndereco: 17) : null;
    }

    /// <summary>Confere se o CPF já existe, ignorando o próprio cliente na edição.</summary>
    public async Task<bool> CpfJaCadastradoAsync(string cpf, int idIgnorado = 0, CancellationToken cancelamento = default)
    {
        const string sql = "SELECT COUNT(*) FROM CLIENTES WHERE CPF = :cpf AND ID <> :idIgnorado";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("cpf", cpf).Param("idIgnorado", idIgnorado);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync(cancelamento)) > 0;
    }

    /// <summary>Grava cliente e endereço na mesma transação e devolve o id gerado.</summary>
    /// <remarks>
    /// Transação porque são duas tabelas: cliente sem endereço é um cadastro pela
    /// metade, e endereço órfão nem existe. Se a segunda gravação falhar, a
    /// primeira volta atrás.
    /// </remarks>
    public async Task<int> CadastrarAsync(Cliente cliente, CancellationToken cancelamento = default)
    {
        const string sqlCliente = """
            INSERT INTO CLIENTES (
                NOME, CPF, EMAIL, WHATSAPP,
                CNH_NUMERO, CNH_CATEGORIA, CNH_VALIDADE, CNH_REGISTRO,
                RG, RG_ORGAO, NACIONALIDADE, ESTADO_CIVIL, PROFISSAO,
                OBSERVACOES, ARQUIVADO)
            VALUES (
                :nome, :cpf, :email, :whatsapp,
                :cnhNumero, :cnhCategoria, :cnhValidade, :cnhRegistro,
                :rg, :rgOrgao, :nacionalidade, :estadoCivil, :profissao,
                :observacoes, :arquivado)
            RETURNING ID INTO :novoId
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var transacao = (OracleTransaction)await conexao.BeginTransactionAsync(cancelamento);

        try
        {
            int idGerado;
            await using (var cmd = conexao.Comando(sqlCliente, transacao))
            {
                PreencherParametros(cmd, cliente);

                var pId = new OracleParameter("novoId", OracleDbType.Int32)
                {
                    Direction = System.Data.ParameterDirection.Output
                };
                cmd.Parameters.Add(pId);

                await cmd.ExecuteNonQueryAsync(cancelamento);
                idGerado = Convert.ToInt32(pId.Value.ToString());
            }

            if (cliente.Endereco is not null)
            {
                await GravarEnderecoAsync(conexao, transacao, idGerado, cliente.Endereco, cancelamento);
            }

            await transacao.CommitAsync(cancelamento);
            cliente.Id = idGerado;
            return idGerado;
        }
        catch
        {
            await transacao.RollbackAsync(cancelamento);
            throw;
        }
    }

    /// <summary>Atualiza cliente e endereço na mesma transação.</summary>
    public async Task<bool> AtualizarAsync(Cliente cliente, CancellationToken cancelamento = default)
    {
        const string sqlCliente = """
            UPDATE CLIENTES SET
                NOME = :nome, CPF = :cpf, EMAIL = :email, WHATSAPP = :whatsapp,
                CNH_NUMERO = :cnhNumero, CNH_CATEGORIA = :cnhCategoria,
                CNH_VALIDADE = :cnhValidade, CNH_REGISTRO = :cnhRegistro,
                RG = :rg, RG_ORGAO = :rgOrgao, NACIONALIDADE = :nacionalidade,
                ESTADO_CIVIL = :estadoCivil, PROFISSAO = :profissao,
                OBSERVACOES = :observacoes, ARQUIVADO = :arquivado
             WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var transacao = (OracleTransaction)await conexao.BeginTransactionAsync(cancelamento);

        try
        {
            int linhas;
            await using (var cmd = conexao.Comando(sqlCliente, transacao))
            {
                PreencherParametros(cmd, cliente);
                cmd.Param("id", cliente.Id);
                linhas = await cmd.ExecuteNonQueryAsync(cancelamento);
            }

            if (cliente.Endereco is not null)
            {
                await GravarEnderecoAsync(conexao, transacao, cliente.Id, cliente.Endereco, cancelamento);
            }

            await transacao.CommitAsync(cancelamento);
            return linhas > 0;
        }
        catch
        {
            await transacao.RollbackAsync(cancelamento);
            throw;
        }
    }

    /// <summary>Arquiva o cliente em vez de apagar.</summary>
    /// <remarks>
    /// Exclusão de verdade não existe no sistema: o histórico de processos e
    /// pagamentos precisa continuar de pé, e um clique errado não pode levar anos
    /// de trabalho junto.
    /// </remarks>
    public async Task<bool> ArquivarAsync(int id, bool arquivar = true, CancellationToken cancelamento = default)
    {
        const string sql = "UPDATE CLIENTES SET ARQUIVADO = :arquivado WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("arquivado", arquivar.ParaSN()).Param("id", id);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    /// <summary>Arquiva vários clientes de uma vez, na mesma transação.</summary>
    public async Task<int> ArquivarVariosAsync(
        IEnumerable<int> ids,
        bool arquivar = true,
        CancellationToken cancelamento = default)
    {
        var lista = ids.Distinct().ToList();
        if (lista.Count == 0) return 0;

        const string sql = "UPDATE CLIENTES SET ARQUIVADO = :arquivado WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var transacao = (OracleTransaction)await conexao.BeginTransactionAsync(cancelamento);

        try
        {
            var afetados = 0;
            await using (var cmd = conexao.Comando(sql, transacao))
            {
                cmd.Param("arquivado", arquivar.ParaSN());
                var pId = new OracleParameter("id", OracleDbType.Int32);
                cmd.Parameters.Add(pId);

                foreach (var id in lista)
                {
                    pId.Value = id;
                    afetados += await cmd.ExecuteNonQueryAsync(cancelamento);
                }
            }

            await transacao.CommitAsync(cancelamento);
            return afetados;
        }
        catch
        {
            await transacao.RollbackAsync(cancelamento);
            throw;
        }
    }

    // ---------------------------------------------------------------------
    // Apoio interno
    // ---------------------------------------------------------------------

    private static async Task GravarEnderecoAsync(
        OracleConnection conexao,
        OracleTransaction transacao,
        int clienteId,
        Endereco endereco,
        CancellationToken cancelamento)
    {
        // MERGE porque o endereço é um por cliente: se já existe, atualiza;
        // se não, cria. Evita o par "conferir antes, gravar depois", que abre
        // brecha para duas telas gravarem ao mesmo tempo.
        const string sql = """
            MERGE INTO ENDERECOS d
            USING (SELECT :clienteId AS CLIENTE_ID FROM DUAL) s
               ON (d.CLIENTE_ID = s.CLIENTE_ID)
            WHEN MATCHED THEN UPDATE SET
                CEP = :cep, LOGRADOURO = :logradouro, NUMERO = :numero,
                COMPLEMENTO = :complemento, BAIRRO = :bairro,
                CIDADE = :cidade, ESTADO = :estado
            WHEN NOT MATCHED THEN INSERT
                (CLIENTE_ID, CEP, LOGRADOURO, NUMERO, COMPLEMENTO, BAIRRO, CIDADE, ESTADO)
                VALUES (:clienteId, :cep, :logradouro, :numero, :complemento, :bairro, :cidade, :estado)
            """;

        await using var cmd = conexao.Comando(sql, transacao);
        cmd.Param("clienteId", clienteId)
           .Param("cep", endereco.Cep)
           .Param("logradouro", endereco.Logradouro)
           .Param("numero", endereco.Numero)
           .Param("complemento", endereco.Complemento)
           .Param("bairro", endereco.Bairro)
           .Param("cidade", endereco.Cidade)
           .Param("estado", endereco.Estado);

        await cmd.ExecuteNonQueryAsync(cancelamento);
    }

    private static async Task<List<VeiculoDoCliente>> ListarVeiculosAsync(
        OracleConnection conexao,
        int clienteId,
        CancellationToken cancelamento)
    {
        const string sql = """
            SELECT CV.ID, CV.VINCULO,
                   V.ID, V.PLACA, V.RENAVAM, V.MARCA_MODELO, V.COR, V.ANO, V.OBSERVACAO
              FROM CLIENTE_VEICULO CV
              JOIN VEICULOS V ON V.ID = CV.VEICULO_ID
             WHERE CV.CLIENTE_ID = :clienteId
             ORDER BY V.PLACA
            """;

        await using var cmd = conexao.Comando(sql);
        cmd.Param("clienteId", clienteId);

        var lista = new List<VeiculoDoCliente>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(new VeiculoDoCliente
            {
                VinculoId = reader.Inteiro(0),
                Vinculo = ValorBanco.De<VinculoVeiculo>(reader.Texto(1)),
                Veiculo = new Veiculo
                {
                    Id = reader.Inteiro(2),
                    Placa = reader.Texto(3),
                    Renavam = reader.TextoOuNulo(4),
                    MarcaModelo = reader.TextoOuNulo(5),
                    Cor = reader.TextoOuNulo(6),
                    Ano = reader.TextoOuNulo(7),
                    Observacao = reader.TextoOuNulo(8)
                }
            });
        }

        return lista;
    }

    private static void PreencherParametros(OracleCommand cmd, Cliente cliente)
    {
        cmd.Param("nome", cliente.Nome)
           .Param("cpf", cliente.Cpf)
           .Param("email", cliente.Email)
           .Param("whatsapp", cliente.Whatsapp)
           .Param("cnhNumero", cliente.CnhNumero)
           .Param("cnhCategoria", cliente.CnhCategoria)
           .Param("cnhValidade", cliente.CnhValidade)
           .Param("cnhRegistro", cliente.CnhRegistro)
           .Param("rg", cliente.Rg)
           .Param("rgOrgao", cliente.RgOrgao)
           .Param("nacionalidade", cliente.Nacionalidade)
           .Param("estadoCivil", cliente.EstadoCivil)
           .Param("profissao", cliente.Profissao)
           .Param("observacoes", cliente.Observacoes)
           .Param("arquivado", cliente.Arquivado.ParaSN());
    }

    /// <summary>Monta o cliente a partir do reader. O endereço começa na coluna informada.</summary>
    private static Cliente LerCliente(OracleDataReader reader, int colunaEndereco)
    {
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

        // Cliente sem endereço: as colunas do LEFT JOIN vêm nulas.
        if (!reader.IsDBNull(colunaEndereco))
        {
            cliente.Endereco = new Endereco
            {
                Id = reader.Inteiro(colunaEndereco),
                ClienteId = cliente.Id,
                Cep = reader.TextoOuNulo(colunaEndereco + 1),
                Logradouro = reader.Texto(colunaEndereco + 2),
                Numero = reader.TextoOuNulo(colunaEndereco + 3),
                Complemento = reader.TextoOuNulo(colunaEndereco + 4),
                Bairro = reader.TextoOuNulo(colunaEndereco + 5),
                Cidade = reader.Texto(colunaEndereco + 6),
                Estado = reader.Texto(colunaEndereco + 7)
            };
        }

        return cliente;
    }
}
