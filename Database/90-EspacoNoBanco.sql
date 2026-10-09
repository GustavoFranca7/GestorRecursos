-- =====================================================================
-- GESTOR DE RECURSOS - quanto espaco o banco esta usando
-- =====================================================================
-- Rodar conectado como GESTOR, em Database actions > SQL (ou no SQL
-- Developer). So le: nao altera nada.
--
-- O Always Free da 20 GB. Rodar isto de vez em quando responde duas
-- perguntas: quanto falta para o limite, e qual tabela esta crescendo.
--
-- Rodar este script tambem conta como atividade: a Oracle para o banco
-- depois de 7 dias sem conexao, e qualquer consulta zera essa contagem.
-- =====================================================================


-- ---------------------------------------------------------------------
-- 1. O total: quanto do limite ja foi usado
-- ---------------------------------------------------------------------
SELECT ROUND(SUM(bytes)/1024/1024, 2)                AS mb_usados,
       ROUND(SUM(bytes)/1024/1024/(20*1024)*100, 3)  AS pct_dos_20gb,
       ROUND(20*1024 - SUM(bytes)/1024/1024, 2)      AS mb_livres
  FROM user_segments;


-- ---------------------------------------------------------------------
-- 2. Por tabela, somando o que pertence a ela
-- ---------------------------------------------------------------------
-- Uma tabela nao ocupa espaco sozinha: os indices e os campos grandes
-- (LOB) dela sao segmentos separados. Esta consulta junta tudo no nome
-- da tabela, que e como se pensa no assunto.
WITH mapa AS (
    SELECT table_name AS tabela, table_name   AS segmento FROM user_tables
    UNION ALL
    SELECT table_name,           index_name            FROM user_indexes
    UNION ALL
    SELECT table_name,           segment_name          FROM user_lobs
    UNION ALL
    SELECT table_name,           index_name            FROM user_lobs
)
SELECT m.tabela,
       ROUND(SUM(s.bytes)/1024/1024, 2) AS mb
  FROM mapa m
  JOIN user_segments s ON s.segment_name = m.segmento
 GROUP BY m.tabela
 ORDER BY SUM(s.bytes) DESC;


-- ---------------------------------------------------------------------
-- 3. Quantas linhas tem cada tabela
-- ---------------------------------------------------------------------
-- Espaco sozinho engana: uma tabela vazia ja reserva o pedaco minimo.
-- Cruzar com a contagem de linhas mostra o que esta crescendo de fato.
--
-- num_rows vem da ultima vez que o Oracle analisou a tabela, entao pode
-- estar atrasado. Para numeros do momento, use COUNT(*) na tabela.
SELECT table_name,
       num_rows,
       last_analyzed
  FROM user_tables
 ORDER BY num_rows DESC NULLS LAST;


-- ---------------------------------------------------------------------
-- 4. O que nao e dado nosso
-- ---------------------------------------------------------------------
-- O Database actions guarda o historico dos comandos digitados na tela
-- de SQL numa tabela propria, e ela chega a pesar mais que o sistema
-- inteiro. Nao atrapalha, mas aparece na conta.
SELECT segment_name,
       segment_type,
       ROUND(bytes/1024/1024, 2) AS mb
  FROM user_segments
 WHERE segment_name LIKE 'DBTOOLS%'
    OR segment_name IN (SELECT segment_name FROM user_lobs
                         WHERE table_name LIKE 'DBTOOLS%')
 ORDER BY bytes DESC;

-- Para esvaziar esse historico (opcional, nao mexe em nada do sistema):
--     DELETE FROM dbtools$execution_history;
--     COMMIT;
