-- =====================================================================
-- 15 - SERIE_ID: o que liga as ocorrencias de uma tarefa que se repete
--
-- Marcar "toda semana ate o ano que vem" grava 52 linhas na AGENDA, e
-- ate aqui nada ligava uma a outra. O "excluir esta e as seguintes"
-- agrupava pela combinacao titulo + recorrencia + cliente, e isso se
-- perdia no momento em que alguem renomeava UMA ocorrencia: ela deixava
-- de bater com as irmas e ficava orfa no calendario.
--
-- SERIE_ID resolve de vez: e gravado quando a serie nasce e nao muda
-- mais, por mais que o titulo, a hora ou o dia sejam corrigidos depois.
--
-- O valor e o ID da primeira ocorrencia. Nao precisa de sequence nova, e
-- o numero ainda diz alguma coisa: "a serie que comecou naquela linha".
-- Se a primeira for apagada, o numero continua servindo de chave - e so
-- um agrupador, nao uma chave estrangeira.
--
-- Compromisso de uma vez so fica com SERIE_ID nulo: ele nao e serie.
--
-- Nao tem GRANT: a tabela AGENDA ja existe e o ESTACAO ja enxerga ela.
-- =====================================================================

-- ---------------------------------------------------------------------
-- 1. A coluna
-- ---------------------------------------------------------------------
ALTER TABLE AGENDA ADD SERIE_ID NUMBER;

COMMENT ON COLUMN AGENDA.SERIE_ID IS
    'Liga as ocorrencias de uma tarefa que se repete. E o ID da primeira delas. Nulo quando nao repete.';


-- ---------------------------------------------------------------------
-- 2. O que ja esta gravado
--
-- As series criadas antes desta coluna sao reagrupadas pela mesma regra
-- que a tela usava: titulo + recorrencia + cliente. Vale aqui, uma vez,
-- porque nenhuma delas foi renomeada ainda - e o que podia dar errado
-- com essa regra e justamente o que a coluna passa a impedir.
--
-- Cliente nulo entra no grupo com NVL: no Oracle, NULL nao e igual a
-- NULL, e sem isso cada tarefa do escritorio viraria uma serie de uma
-- linha so.
-- ---------------------------------------------------------------------
UPDATE AGENDA A
   SET A.SERIE_ID = (
        SELECT MIN(B.ID)
          FROM AGENDA B
         WHERE B.TITULO       = A.TITULO
           AND B.RECORRENCIA  = A.RECORRENCIA
           AND NVL(B.CLIENTE_ID, -1) = NVL(A.CLIENTE_ID, -1))
 WHERE A.RECORRENCIA <> 'NENHUMA';

COMMIT;


-- ---------------------------------------------------------------------
-- 3. O indice
--
-- Apagar uma serie busca por SERIE_ID; sem indice seria varredura da
-- tabela inteira a cada exclusao.
-- ---------------------------------------------------------------------
CREATE INDEX IX_AGENDA_SERIE ON AGENDA (SERIE_ID);


-- ---------------------------------------------------------------------
-- 4. Conferencia
--
-- A primeira consulta deve voltar vazia: toda linha que se repete tem
-- de ter serie. A segunda mostra as series que existem hoje.
-- ---------------------------------------------------------------------
SELECT ID, DATA_AGENDA, TITULO, RECORRENCIA
  FROM AGENDA
 WHERE RECORRENCIA <> 'NENHUMA'
   AND SERIE_ID IS NULL;

SELECT SERIE_ID,
       COUNT(*)           AS OCORRENCIAS,
       MIN(DATA_AGENDA)   AS PRIMEIRA,
       MAX(DATA_AGENDA)   AS ULTIMA,
       MIN(TITULO)        AS TITULO
  FROM AGENDA
 WHERE SERIE_ID IS NOT NULL
 GROUP BY SERIE_ID
 ORDER BY PRIMEIRA;
