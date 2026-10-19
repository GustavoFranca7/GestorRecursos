using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: as fases do recurso e os prazos que não podem ser perdidos.
public class InstanciaDAO
{
    private const string Colunas = """
        I.ID, I.ITEM_ID, I.FASE, I.SITUACAO, I.PRAZO_PROTOCOLO, I.DATA_PROTOCOLO,
        I.NUMERO_PROTOCOLO, I.ORGAO_DESTINATARIO, I.DATA_RESULTADO, I.RESULTADO, I.OBSERVACOES
        """;

    /// <summary>Prazos que vencem até a data informada e ainda não foram protocolados.</summary>
    /// <remarks>
    /// É a consulta do painel: "o que precisa ser protocolado". Traz o cliente
    /// junto porque a tela sempre mostra de quem é o prazo.
    /// </remarks>
    public async Task<List<(Instancia Instancia, string ClienteNome, int ClienteId, string Referencia)>> ListarPrazosAteAsync(
        DateTime limite,
        CancellationToken cancelamento = default)
    {
        var sql = $"""
            SELECT {Colunas}, C.NOME, C.ID, NVL(M.AIT, P.NUMERO_PROCESSO)
              FROM INSTANCIAS I
              JOIN ITENS_CONTRATACAO IT ON IT.ID = I.ITEM_ID
              JOIN CONTRATACOES CT ON CT.ID = IT.CONTRATACAO_ID
              JOIN CLIENTES C ON C.ID = CT.CLIENTE_ID
              LEFT JOIN MULTAS M ON M.ID = IT.MULTA_ID
              LEFT JOIN PROCESSOS_CNH P ON P.ID = IT.PROCESSO_CNH_ID
             WHERE I.DATA_PROTOCOLO IS NULL
               AND I.PRAZO_PROTOCOLO IS NOT NULL
               AND I.PRAZO_PROTOCOLO <= :limite
               AND I.SITUACAO NOT IN ('CANCELADO', 'DESISTENCIA', 'DEFERIDO')
             ORDER BY I.PRAZO_PROTOCOLO
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("limite", limite.Date);

        var lista = new List<(Instancia, string, int, string)>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add((Ler(reader), reader.Texto(11), reader.Inteiro(12), reader.Texto(13)));
        }

        return lista;
    }

    public async Task<List<Instancia>> ListarPorItemAsync(int itemId, CancellationToken cancelamento = default)
    {
        var sql = $"SELECT {Colunas} FROM INSTANCIAS I WHERE I.ITEM_ID = :itemId ORDER BY I.ID";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("itemId", itemId);

        var lista = new List<Instancia>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(Ler(reader));
        }

        return lista;
    }

    /// <summary>Registra o protocolo de uma fase. É o que desliga o alarme do prazo.</summary>
    public async Task<bool> RegistrarProtocoloAsync(
        int instanciaId,
        DateTime dataProtocolo,
        string numeroProtocolo,
        string? orgaoDestinatario = null,
        CancellationToken cancelamento = default)
    {
        // O destinatário só é escrito quando veio preenchido: regravar nulo em cima
        // do que já estava lá apagaria informação em quem só corrige o número.
        const string sql = """
            UPDATE INSTANCIAS SET
                DATA_PROTOCOLO = :dataProtocolo,
                NUMERO_PROTOCOLO = :numeroProtocolo,
                ORGAO_DESTINATARIO = NVL(:orgaoDestinatario, ORGAO_DESTINATARIO),
                SITUACAO = 'AGUARDANDO_JULGAMENTO'
             WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("dataProtocolo", dataProtocolo.Date)
           .Param("numeroProtocolo", numeroProtocolo)
           .Param("orgaoDestinatario", orgaoDestinatario)
           .Param("id", instanciaId);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    /// <summary>Grava as três fases de uma vez, como elas vieram da tela de edição.</summary>
    /// <remarks>
    /// O caminho de sempre — protocolar, esperar, registrar o resultado, repetir — serve
    /// ao recurso que nasce aqui e vai andando. Não serve ao recurso que chega pronto:
    /// um processo que já passou pela autuação e pela JARI exigia seis janelas seguidas
    /// para ser lançado. Esta porta existe para esse caso.
    ///
    /// Tudo numa transação: um recurso com a autuação indeferida e a JARI não criada é
    /// pior que nenhum, porque some do radar sem ninguém perceber.
    ///
    /// A fase seguinte nasce quando a anterior indefere, com ou sem dados — é o que a
    /// mantém visível na tela, esperando protocolo.
    /// </remarks>
    public async Task<bool> SalvarAndamentoAsync(
        int itemId,
        IReadOnlyList<Instancia> fases,
        CancellationToken cancelamento = default)
    {
        const string sqlAtualizar = """
            UPDATE INSTANCIAS SET
                SITUACAO = :situacao,
                PRAZO_PROTOCOLO = :prazoProtocolo,
                DATA_PROTOCOLO = :dataProtocolo,
                NUMERO_PROTOCOLO = :numeroProtocolo,
                ORGAO_DESTINATARIO = :orgaoDestinatario,
                DATA_RESULTADO = :dataResultado,
                RESULTADO = :resultado,
                OBSERVACOES = :observacoes
             WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var transacao = (OracleTransaction)await conexao.BeginTransactionAsync(cancelamento);

        try
        {
            var indeferiuAnterior = false;

            foreach (var fase in fases.OrderBy(f => f.Fase))
            {
                // A fase entra se já existe, se tem algo preenchido, ou se a anterior
                // indeferiu — nesse caso ela precisa existir mesmo vazia.
                var precisa = fase.Id > 0 || fase.TemConteudo || indeferiuAnterior;
                if (!precisa)
                {
                    continue;
                }

                fase.ItemId = itemId;
                fase.Situacao = fase.SituacaoPeloPreenchido();

                if (fase.Id > 0)
                {
                    await using var cmd = conexao.Comando(sqlAtualizar, transacao);
                    cmd.Param("situacao", fase.Situacao.Texto())
                       .Param("prazoProtocolo", fase.PrazoProtocolo)
                       .Param("dataProtocolo", fase.DataProtocolo)
                       .Param("numeroProtocolo", fase.NumeroProtocolo)
                       .Param("orgaoDestinatario", fase.OrgaoDestinatario)
                       .Param("dataResultado", fase.DataResultado)
                       .Param("resultado", fase.Resultado?.Texto())
                       .Param("observacoes", fase.Observacoes)
                       .Param("id", fase.Id);

                    await cmd.ExecuteNonQueryAsync(cancelamento);
                }
                else
                {
                    await InserirAsync(conexao, transacao, fase, cancelamento);
                }

                indeferiuAnterior = fase.Resultado == ResultadoInstancia.Indeferido;
            }

            await transacao.CommitAsync(cancelamento);
            return true;
        }
        catch
        {
            await transacao.RollbackAsync(cancelamento);
            throw;
        }
    }

    /// <summary>Encerra o recurso: o item sai de andamento e as fases abertas param.</summary>
    /// <remarks>
    /// É a desistência, ou o conserto de um recurso lançado por engano. Não apaga nada —
    /// a contratação continua lá, com o valor que foi cobrado, porque o dinheiro é
    /// histórico. O que muda é que ninguém mais espera trabalho deste item.
    ///
    /// Fase já julgada fica como está: deferido no passado não vira cancelado.
    /// </remarks>
    public async Task<bool> CancelarRecursoAsync(int itemId, CancellationToken cancelamento = default)
    {
        const string sqlFases = """
            UPDATE INSTANCIAS SET SITUACAO = 'CANCELADO'
             WHERE ITEM_ID = :itemId
               AND SITUACAO NOT IN ('DEFERIDO','INDEFERIDO','CANCELADO','DESISTENCIA')
            """;

        const string sqlItem = """
            UPDATE ITENS_CONTRATACAO SET SITUACAO = 'CANCELADO' WHERE ID = :itemId
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var transacao = (OracleTransaction)await conexao.BeginTransactionAsync(cancelamento);

        try
        {
            await using (var cmd = conexao.Comando(sqlFases, transacao))
            {
                cmd.Param("itemId", itemId);
                await cmd.ExecuteNonQueryAsync(cancelamento);
            }

            int afetadas;
            await using (var cmd = conexao.Comando(sqlItem, transacao))
            {
                cmd.Param("itemId", itemId);
                afetadas = await cmd.ExecuteNonQueryAsync(cancelamento);
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

    /// <summary>Registra o resultado e, quando indeferido, já abre a fase seguinte.</summary>
    /// <remarks>
    /// O valor cobrado inclui as três instâncias, então subir de fase não é
    /// decisão comercial: é a continuação natural do serviço. Fazer isso na mesma
    /// transação evita o caso de o resultado ser gravado e a próxima fase não.
    /// </remarks>
    public async Task<bool> RegistrarResultadoAsync(
        int instanciaId,
        ResultadoInstancia resultado,
        DateTime dataResultado,
        DateTime? prazoProximaFase = null,
        CancellationToken cancelamento = default)
    {
        const string sqlAtual = """
            UPDATE INSTANCIAS SET
                DATA_RESULTADO = :dataResultado,
                RESULTADO = :resultado,
                SITUACAO = :situacao
             WHERE ID = :id
            """;

        const string sqlDadosAtual = """
            SELECT ITEM_ID, FASE FROM INSTANCIAS WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var transacao = (OracleTransaction)await conexao.BeginTransactionAsync(cancelamento);

        try
        {
            int itemId;
            FaseInstancia faseAtual;

            await using (var cmd = conexao.Comando(sqlDadosAtual, transacao))
            {
                cmd.Param("id", instanciaId);
                await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
                if (!await reader.ReadAsync(cancelamento))
                {
                    await transacao.RollbackAsync(cancelamento);
                    return false;
                }

                itemId = reader.Inteiro(0);
                faseAtual = ValorBanco.De<FaseInstancia>(reader.Texto(1));
            }

            await using (var cmd = conexao.Comando(sqlAtual, transacao))
            {
                cmd.Param("dataResultado", dataResultado.Date)
                   .Param("resultado", resultado.Texto())
                   .Param("situacao", resultado == ResultadoInstancia.Deferido
                        ? SituacaoInstancia.Deferido.Texto()
                        : SituacaoInstancia.Indeferido.Texto())
                   .Param("id", instanciaId);

                await cmd.ExecuteNonQueryAsync(cancelamento);
            }

            var proxima = ProximaFase(faseAtual);
            if (resultado == ResultadoInstancia.Indeferido && proxima is not null)
            {
                await InserirAsync(conexao, transacao, new Instancia
                {
                    ItemId = itemId,
                    Fase = proxima.Value,
                    Situacao = proxima == FaseInstancia.Jari
                        ? SituacaoInstancia.AguardandoProtocoloJari
                        : SituacaoInstancia.AguardandoProtocoloCetran,
                    PrazoProtocolo = prazoProximaFase
                }, cancelamento);
            }

            await transacao.CommitAsync(cancelamento);
            return true;
        }
        catch
        {
            await transacao.RollbackAsync(cancelamento);
            throw;
        }
    }

    public async Task<bool> AtualizarSituacaoAsync(
        int instanciaId,
        SituacaoInstancia situacao,
        CancellationToken cancelamento = default)
    {
        const string sql = "UPDATE INSTANCIAS SET SITUACAO = :situacao WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("situacao", situacao.Texto()).Param("id", instanciaId);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    // ---------------------------------------------------------------------
    // Usados pelo ContratacaoDAO dentro da transação dele.
    // ---------------------------------------------------------------------

    internal static async Task InserirAsync(
        OracleConnection conexao,
        OracleTransaction? transacao,
        Instancia instancia,
        CancellationToken cancelamento)
    {
        const string sql = """
            INSERT INTO INSTANCIAS
                (ITEM_ID, FASE, SITUACAO, PRAZO_PROTOCOLO, DATA_PROTOCOLO,
                 NUMERO_PROTOCOLO, ORGAO_DESTINATARIO, DATA_RESULTADO, RESULTADO, OBSERVACOES)
            VALUES
                (:itemId, :fase, :situacao, :prazoProtocolo, :dataProtocolo,
                 :numeroProtocolo, :orgaoDestinatario, :dataResultado, :resultado, :observacoes)
            """;

        await using var cmd = conexao.Comando(sql, transacao);
        cmd.Param("itemId", instancia.ItemId)
           .Param("fase", instancia.Fase.Texto())
           .Param("situacao", instancia.Situacao.Texto())
           .Param("prazoProtocolo", instancia.PrazoProtocolo)
           .Param("dataProtocolo", instancia.DataProtocolo)
           .Param("numeroProtocolo", instancia.NumeroProtocolo)
           .Param("orgaoDestinatario", instancia.OrgaoDestinatario)
           .Param("dataResultado", instancia.DataResultado)
           .Param("resultado", instancia.Resultado?.Texto())
           .Param("observacoes", instancia.Observacoes);

        await cmd.ExecuteNonQueryAsync(cancelamento);
    }

    internal static async Task<List<Instancia>> ListarPorContratacaoAsync(
        OracleConnection conexao,
        int contratacaoId,
        CancellationToken cancelamento)
    {
        var sql = $"""
            SELECT {Colunas}
              FROM INSTANCIAS I
              JOIN ITENS_CONTRATACAO IT ON IT.ID = I.ITEM_ID
             WHERE IT.CONTRATACAO_ID = :contratacaoId
             ORDER BY I.ITEM_ID, I.ID
            """;

        await using var cmd = conexao.Comando(sql);
        cmd.Param("contratacaoId", contratacaoId);

        var lista = new List<Instancia>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            lista.Add(Ler(reader));
        }

        return lista;
    }

    private static FaseInstancia? ProximaFase(FaseInstancia atual) => atual switch
    {
        FaseInstancia.Autuacao => FaseInstancia.Jari,
        FaseInstancia.Jari => FaseInstancia.Cetran,
        _ => null // CETRAN é a última: depois dela só resta a via judicial.
    };

    private static Instancia Ler(OracleDataReader reader) => new()
    {
        Id = reader.Inteiro(0),
        ItemId = reader.Inteiro(1),
        Fase = ValorBanco.De<FaseInstancia>(reader.Texto(2)),
        Situacao = ValorBanco.De<SituacaoInstancia>(reader.Texto(3)),
        PrazoProtocolo = reader.DataOuNulo(4),
        DataProtocolo = reader.DataOuNulo(5),
        NumeroProtocolo = reader.TextoOuNulo(6),
        OrgaoDestinatario = reader.TextoOuNulo(7),
        DataResultado = reader.DataOuNulo(8),
        Resultado = ValorBanco.DeOuNulo<ResultadoInstancia>(reader.TextoOuNulo(9)),
        Observacoes = reader.TextoOuNulo(10)
    };
}
