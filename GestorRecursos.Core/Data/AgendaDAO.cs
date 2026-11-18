using GestorRecursos.Core.Models;
using Oracle.ManagedDataAccess.Client;

namespace GestorRecursos.Core.Data;

// Camada Core / Data: a agenda que substitui a planilha de produção diária.
public class AgendaDAO
{
    private const string Colunas = """
        A.ID, A.DATA_AGENDA, A.HORA, A.TIPO, A.TITULO, A.CLIENTE_ID,
        A.INSTANCIA_ID, A.CONCLUIDO, A.RECORRENCIA, A.OBSERVACAO, A.TIPO_OUTRO,
        A.SERIE_ID
        """;

    /// <summary>Compromissos de um período, com o nome do cliente quando houver.</summary>
    /// <remarks>
    /// A visão padrão dela é o mês inteiro: "às vezes tem 1 dia com 15 recursos,
    /// e assim consigo ter uma noção futura melhor".
    /// </remarks>
    public async Task<List<Compromisso>> ListarPeriodoAsync(
        DateTime inicio,
        DateTime fim,
        CancellationToken cancelamento = default)
    {
        var sql = $"""
            SELECT {Colunas}, C.NOME
              FROM AGENDA A
              LEFT JOIN CLIENTES C ON C.ID = A.CLIENTE_ID
             WHERE A.DATA_AGENDA BETWEEN :inicio AND :fim
             ORDER BY A.DATA_AGENDA, A.HORA NULLS FIRST, A.ID
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("inicio", inicio.Date).Param("fim", fim.Date);

        var lista = new List<Compromisso>();
        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        while (await reader.ReadAsync(cancelamento))
        {
            var compromisso = Ler(reader);
            compromisso.ClienteNome = reader.TextoOuNulo(12);
            lista.Add(compromisso);
        }

        return lista;
    }

    public async Task<List<Compromisso>> ListarDoMesAsync(int ano, int mes, CancellationToken cancelamento = default)
    {
        var inicio = new DateTime(ano, mes, 1);
        return await ListarPeriodoAsync(inicio, inicio.AddMonths(1).AddDays(-1), cancelamento);
    }

    public async Task<int> CriarAsync(Compromisso compromisso, CancellationToken cancelamento = default)
    {
        const string sql = """
            INSERT INTO AGENDA
                (DATA_AGENDA, HORA, TIPO, TITULO, CLIENTE_ID, INSTANCIA_ID,
                 CONCLUIDO, RECORRENCIA, OBSERVACAO, TIPO_OUTRO)
            VALUES
                (:data, :hora, :tipo, :titulo, :clienteId, :instanciaId,
                 :concluido, :recorrencia, :observacao, :tipoOutro)
            RETURNING ID INTO :novoId
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("data", compromisso.Data.Date)
           .Param("hora", compromisso.Hora)
           .Param("tipo", compromisso.Tipo.Texto())
           .Param("titulo", compromisso.Titulo)
           .Param("clienteId", compromisso.ClienteId)
           .Param("instanciaId", compromisso.InstanciaId)
           .Param("concluido", compromisso.Concluido.ParaSN())
           .Param("recorrencia", compromisso.Recorrencia.Texto())
           .Param("observacao", compromisso.Observacao)
           .Param("tipoOutro", compromisso.TipoOutro);

        var pId = new OracleParameter("novoId", OracleDbType.Int32)
        {
            Direction = System.Data.ParameterDirection.Output
        };
        cmd.Parameters.Add(pId);

        await cmd.ExecuteNonQueryAsync(cancelamento);
        compromisso.Id = Convert.ToInt32(pId.Value.ToString());
        return compromisso.Id;
    }

    public async Task<bool> ConcluirAsync(int id, bool concluido = true, CancellationToken cancelamento = default)
    {
        const string sql = "UPDATE AGENDA SET CONCLUIDO = :concluido WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("concluido", concluido.ParaSN()).Param("id", id);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }
    /// <summary>Cria a série inteira de uma vez: a data dada e todas as repetições até o fim.</summary>
    /// <remarks>
    /// A primeira versão criava só a próxima ocorrência, quando a anterior era concluída.
    /// Economizava linhas, mas quem marca "toda semana" espera ver a tarefa em todas as
    /// semanas ao abrir o mês — e não só depois de ter marcado a de hoje como feita. A
    /// agenda existe para mostrar carga futura; série que só aparece aos poucos não mostra.
    ///
    /// Tudo numa transação: meia série gravada deixaria a tarefa em algumas semanas e não
    /// em outras, sem nada na tela dizendo por quê.
    /// </remarks>
    /// <returns>Quantas ocorrências foram criadas.</returns>
    public async Task<int> CriarSerieAsync(
        Compromisso modelo,
        DateTime ate,
        CancellationToken cancelamento = default)
    {
        // A primeira ocorrência devolve o ID, que vira o SERIE_ID de todas — inclusive
        // dela mesma, no UPDATE logo abaixo. Evita criar uma sequence só para isto, e o
        // número ainda diz algo: a série que começou naquela linha.
        const string sqlPrimeira = """
            INSERT INTO AGENDA
                (DATA_AGENDA, HORA, TIPO, TITULO, CLIENTE_ID, INSTANCIA_ID,
                 CONCLUIDO, RECORRENCIA, OBSERVACAO, TIPO_OUTRO)
            VALUES
                (:data, :hora, :tipo, :titulo, :clienteId, :instanciaId,
                 'N', :recorrencia, :observacao, :tipoOutro)
            RETURNING ID INTO :novoId
            """;

        const string sqlDemais = """
            INSERT INTO AGENDA
                (DATA_AGENDA, HORA, TIPO, TITULO, CLIENTE_ID, INSTANCIA_ID,
                 CONCLUIDO, RECORRENCIA, OBSERVACAO, TIPO_OUTRO, SERIE_ID)
            VALUES
                (:data, :hora, :tipo, :titulo, :clienteId, :instanciaId,
                 'N', :recorrencia, :observacao, :tipoOutro, :serieId)
            """;

        const string sqlMarcarPrimeira = "UPDATE AGENDA SET SERIE_ID = :id WHERE ID = :id";

        var datas = Datas(modelo.Data.Date, ate.Date, modelo.Recorrencia).ToList();
        var repete = modelo.Recorrencia != Recorrencia.Nenhuma;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var transacao = (OracleTransaction)await conexao.BeginTransactionAsync(cancelamento);

        try
        {
            var serieId = 0;

            for (var i = 0; i < datas.Count; i++)
            {
                var primeira = i == 0;

                await using var cmd = conexao.Comando(primeira ? sqlPrimeira : sqlDemais, transacao);
                cmd.Param("data", datas[i])
                   .Param("hora", modelo.Hora)
                   .Param("tipo", modelo.Tipo.Texto())
                   .Param("titulo", modelo.Titulo)
                   .Param("clienteId", modelo.ClienteId)
                   .Param("instanciaId", modelo.InstanciaId)
                   .Param("recorrencia", modelo.Recorrencia.Texto())
                   .Param("observacao", modelo.Observacao)
                   .Param("tipoOutro", modelo.TipoOutro);

                if (primeira)
                {
                    var pId = new OracleParameter("novoId", OracleDbType.Int32)
                    {
                        Direction = System.Data.ParameterDirection.Output
                    };
                    cmd.Parameters.Add(pId);

                    await cmd.ExecuteNonQueryAsync(cancelamento);
                    serieId = Convert.ToInt32(pId.Value.ToString());

                    // Compromisso de uma vez só não é série: fica sem SERIE_ID.
                    if (!repete)
                    {
                        break;
                    }

                    await using var marcar = conexao.Comando(sqlMarcarPrimeira, transacao);
                    marcar.Param("id", serieId);
                    await marcar.ExecuteNonQueryAsync(cancelamento);

                    continue;
                }

                cmd.Param("serieId", serieId);
                await cmd.ExecuteNonQueryAsync(cancelamento);
            }

            await transacao.CommitAsync(cancelamento);
            return datas.Count;
        }
        catch
        {
            await transacao.RollbackAsync(cancelamento);
            throw;
        }
    }

    /// <summary>Quantas ocorrências uma série pode ter.</summary>
    /// <remarks>
    /// Trava contra o engano de digitação: "toda semana até 2099" seriam quatro mil linhas
    /// criadas sem querer, e desfazer isso na mão seria pior que o erro.
    /// </remarks>
    public const int LimiteDaSerie = 200;

    /// <summary>As datas da série, da primeira até o limite escolhido.</summary>
    /// <remarks>
    /// Cada data sai do <b>primeiro dia</b>, somando n de uma vez, e não da anterior.
    /// Somando de uma em uma, "todo dia 31" escorrega e não volta mais: 31/12 vira 31/01,
    /// que vira 28/02 — e daí em diante a série fica presa no dia 28. Contando sempre a
    /// partir da origem, fevereiro encolhe sozinho e março volta ao 31.
    /// </remarks>
    public static IEnumerable<DateTime> Datas(DateTime inicio, DateTime ate, Recorrencia recorrencia)
    {
        var primeiro = inicio.Date;
        var fim = ate.Date;

        // Sem repetição a "série" é o próprio dia: assim quem cria um compromisso
        // simples passa pelo mesmo caminho, e há um lugar só para gravar.
        if (recorrencia == Recorrencia.Nenhuma)
        {
            yield return primeiro;
            yield break;
        }

        for (var n = 0; n < LimiteDaSerie; n++)
        {
            var data = recorrencia switch
            {
                Recorrencia.Semanal => primeiro.AddDays(7 * n),
                Recorrencia.Quinzenal => primeiro.AddDays(15 * n),
                Recorrencia.Mensal => primeiro.AddMonths(n),
                _ => primeiro
            };

            if (data > fim)
            {
                yield break;
            }

            yield return data;
        }
    }

    /// <summary>Altera um compromisso já marcado. Mexe só na ocorrência indicada.</summary>
    /// <remarks>
    /// A recorrência não se altera aqui: mudar "toda semana" para "todo mês" numa linha
    /// não diria o que fazer com as outras cinquenta já gravadas. Trocar o ritmo é
    /// apagar a série e marcar de novo.
    /// </remarks>
    public async Task<bool> AtualizarAsync(Compromisso compromisso, CancellationToken cancelamento = default)
    {
        const string sql = """
            UPDATE AGENDA
               SET DATA_AGENDA = :data,
                   HORA        = :hora,
                   TIPO        = :tipo,
                   TITULO      = :titulo,
                   CLIENTE_ID  = :clienteId,
                   OBSERVACAO  = :observacao,
                   TIPO_OUTRO  = :tipoOutro
             WHERE ID = :id
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("data", compromisso.Data.Date)
           .Param("hora", compromisso.Hora)
           .Param("tipo", compromisso.Tipo.Texto())
           .Param("titulo", compromisso.Titulo)
           .Param("clienteId", compromisso.ClienteId)
           .Param("observacao", compromisso.Observacao)
           .Param("tipoOutro", compromisso.TipoOutro)
           .Param("id", compromisso.Id);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    /// <summary>Apaga desta data em diante todas as ocorrências da mesma série.</summary>
    /// <remarks>
    /// Pelo <c>SERIE_ID</c>, que é gravado quando a série nasce e não muda mais. A versão
    /// anterior agrupava por título, recorrência e cliente, e isso se perdia no momento em
    /// que alguém renomeava uma ocorrência: ela deixava de bater com as irmãs e ficava
    /// órfã no calendário.
    ///
    /// Só daqui para a frente: o que já passou é histórico, e apagar o que foi feito para
    /// parar de repetir no futuro seria perder registro de trabalho.
    /// </remarks>
    public async Task<int> ExcluirSerieAsync(
        int serieId,
        DateTime apartirDe,
        CancellationToken cancelamento = default)
    {
        const string sql = """
            DELETE FROM AGENDA
             WHERE SERIE_ID = :serieId
               AND DATA_AGENDA >= :data
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("serieId", serieId).Param("data", apartirDe.Date);

        return await cmd.ExecuteNonQueryAsync(cancelamento);
    }

    public async Task<bool> ExcluirAsync(int id, CancellationToken cancelamento = default)
    {
        const string sql = "DELETE FROM AGENDA WHERE ID = :id";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("id", id);

        return await cmd.ExecuteNonQueryAsync(cancelamento) > 0;
    }

    // ---------------------------------------------------------------------
    // O mês montado: compromissos + prazos + diários
    // ---------------------------------------------------------------------

    // Os prazos vem das INSTANCIAS, e nao da AGENDA. Entram os ja protocolados
    // tambem: sem eles o "6 de 10" do plano do dia encolheria conforme ela trabalha,
    // e o numero que deveria medir o dia mediria so o que falta.
    // A situação da instância vem junto, e a fase encerrada NÃO é filtrada fora: o
    // recurso deferido sumia do calendário no dia em que era resolvido, e quem voltasse
    // ao dia não encontrava nada — parecia que o trabalho não tinha sido feito. Agora
    // ele fica no lugar, marcado como concluído, dizendo como terminou.
    private const string SqlPrazos = """
        SELECT I.ID, I.PRAZO_PROTOCOLO, I.FASE, TS.NOME,
               CO.CLIENTE_ID, CL.NOME,
               NVL(M.AIT, PC.NUMERO_PROCESSO),
               CASE WHEN I.DATA_PROTOCOLO IS NULL THEN 'N' ELSE 'S' END,
               I.ORGAO_DESTINATARIO,
               I.SITUACAO
          FROM INSTANCIAS I
          JOIN ITENS_CONTRATACAO IT  ON IT.ID = I.ITEM_ID
          JOIN CONTRATACOES CO       ON CO.ID = IT.CONTRATACAO_ID
          JOIN CLIENTES CL           ON CL.ID = CO.CLIENTE_ID
          JOIN TIPOS_SERVICO TS      ON TS.ID = IT.TIPO_SERVICO_ID
          LEFT JOIN MULTAS M         ON M.ID  = IT.MULTA_ID
          LEFT JOIN VEICULOS V       ON V.ID  = M.VEICULO_ID
          LEFT JOIN PROCESSOS_CNH PC ON PC.ID = IT.PROCESSO_CNH_ID
         WHERE I.PRAZO_PROTOCOLO BETWEEN :inicio AND :fim
           AND IT.SITUACAO <> 'CANCELADO'
           AND CO.SITUACAO <> 'CANCELADO'
           AND CL.ARQUIVADO = 'N'
         ORDER BY I.PRAZO_PROTOCOLO, CL.NOME
        """;

    private const string SqlDiarios = """
        SELECT DATA_DIA, ANOTACAO FROM DIARIO_DIA
         WHERE DATA_DIA BETWEEN :inicio AND :fim
        """;

    /// <summary>O mês inteiro: o que foi marcado, o que vence e o que foi anotado.</summary>
    /// <remarks>
    /// Três consultas numa conexão só. O mês é a visão padrão (RF-25), e trocar de mês
    /// é o gesto frequente: fazer uma ida ao banco por dia visível seria trinta idas.
    /// </remarks>
    public async Task<MesDaAgenda> MontarMesAsync(
        int ano, int mes, CancellationToken cancelamento = default)
    {
        var inicio = new DateTime(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var sqlCompromissos = $"""
            SELECT {Colunas}, C.NOME
              FROM AGENDA A
              LEFT JOIN CLIENTES C ON C.ID = A.CLIENTE_ID
             WHERE A.DATA_AGENDA BETWEEN :inicio AND :fim
             ORDER BY A.DATA_AGENDA, A.HORA NULLS FIRST, A.ID
            """;

        await using var conexao = await Database.AbrirAsync(cancelamento);

        var itens = new List<ItemDaAgenda>();

        await using (var cmd = conexao.Comando(sqlCompromissos))
        {
            cmd.Param("inicio", inicio).Param("fim", fim);

            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                var compromisso = Ler(reader);

                itens.Add(new ItemDaAgenda
                {
                    Id = compromisso.Id,
                    Origem = OrigemDoItem.Compromisso,
                    Data = compromisso.Data,
                    Hora = compromisso.Hora,
                    Tipo = compromisso.Tipo,
                    Titulo = compromisso.Titulo,
                    ClienteId = compromisso.ClienteId,
                    ClienteNome = reader.TextoOuNulo(12),
                    Concluido = compromisso.Concluido,
                    Recorrencia = compromisso.Recorrencia,
                    Observacao = compromisso.Observacao,
                    TipoOutro = compromisso.TipoOutro,
                    SerieId = compromisso.SerieId
                });
            }
        }

        await using (var cmd = conexao.Comando(SqlPrazos))
        {
            cmd.Param("inicio", inicio).Param("fim", fim);

            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                itens.Add(new ItemDaAgenda
                {
                    Id = reader.Inteiro(0),
                    Origem = OrigemDoItem.Prazo,
                    Data = reader.Data(1),
                    Tipo = TipoCompromisso.Prazo,
                    // Fase e serviço vão separados: juntar os dois numa frase é escrever
                    // texto de tela, e isso é da camada de cima.
                    Fase = reader.Texto(2),
                    Titulo = reader.Texto(3),
                    ClienteId = reader.InteiroOuNulo(4),
                    ClienteNome = reader.TextoOuNulo(5),
                    Referencia = reader.TextoOuNulo(6),

                    // Concluído é "não pede mais nada": protocolado, ou fase que já
                    // terminou. Prazo perdido entra aqui também — ruim, mas resolvido:
                    // deixá-lo pendente faria o plano do dia nunca fechar.
                    Concluido = reader.Booleano(7) || SituacoesSql.Encerrada(reader.Texto(9)),

                    Observacao = reader.TextoOuNulo(8),
                    Situacao = reader.Texto(9)
                });
            }
        }

        var diarios = new Dictionary<DateTime, string>();
        await using (var cmd = conexao.Comando(SqlDiarios))
        {
            cmd.Param("inicio", inicio).Param("fim", fim);

            await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
            while (await reader.ReadAsync(cancelamento))
            {
                diarios[reader.Data(0).Date] = reader.Texto(1);
            }
        }

        return new MesDaAgenda { Ano = ano, Mes = mes, Itens = itens, Diarios = diarios };
    }

    // ---------------------------------------------------------------------
    // Diário do dia
    // ---------------------------------------------------------------------

    /// <summary>Grava a anotação do dia, criando ou substituindo a que existir.</summary>
    public async Task GravarAnotacaoAsync(DateTime dia, string texto, CancellationToken cancelamento = default)
    {
        // Apagar o texto apaga a linha. No Oracle a string vazia é NULL, e ANOTACAO é
        // NOT NULL: gravá-la daria erro de constraint em vez de limpar o dia.
        const string sqlApagar = "DELETE FROM DIARIO_DIA WHERE DATA_DIA = :dia";

        const string sqlGravar = """
            MERGE INTO DIARIO_DIA d
            USING (SELECT :dia AS DATA_DIA FROM DUAL) s
               ON (d.DATA_DIA = s.DATA_DIA)
            WHEN MATCHED THEN UPDATE SET ANOTACAO = :texto
            WHEN NOT MATCHED THEN INSERT (DATA_DIA, ANOTACAO) VALUES (:dia, :texto)
            """;

        var vazio = string.IsNullOrWhiteSpace(texto);

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(vazio ? sqlApagar : sqlGravar);
        cmd.Param("dia", dia.Date);

        if (!vazio)
        {
            cmd.Param("texto", texto);
        }

        await cmd.ExecuteNonQueryAsync(cancelamento);
    }

    public async Task<AnotacaoDiaria?> BuscarAnotacaoAsync(DateTime dia, CancellationToken cancelamento = default)
    {
        const string sql = "SELECT ID, DATA_DIA, ANOTACAO FROM DIARIO_DIA WHERE DATA_DIA = :dia";

        await using var conexao = await Database.AbrirAsync(cancelamento);
        await using var cmd = conexao.Comando(sql);
        cmd.Param("dia", dia.Date);

        await using var reader = (OracleDataReader)await cmd.ExecuteReaderAsync(cancelamento);
        if (!await reader.ReadAsync(cancelamento))
        {
            return null;
        }

        return new AnotacaoDiaria
        {
            Id = reader.Inteiro(0),
            Data = reader.Data(1),
            Anotacao = reader.Texto(2)
        };
    }

    private static Compromisso Ler(OracleDataReader reader) => new()
    {
        Id = reader.Inteiro(0),
        Data = reader.Data(1),
        Hora = reader.TextoOuNulo(2),
        Tipo = ValorBanco.De<TipoCompromisso>(reader.Texto(3)),
        Titulo = reader.Texto(4),
        ClienteId = reader.InteiroOuNulo(5),
        InstanciaId = reader.InteiroOuNulo(6),
        Concluido = reader.Booleano(7),
        Recorrencia = ValorBanco.De<Recorrencia>(reader.Texto(8)),
        Observacao = reader.TextoOuNulo(9),
        TipoOutro = reader.TextoOuNulo(10),
        SerieId = reader.InteiroOuNulo(11)
    };
}
