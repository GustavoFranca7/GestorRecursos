-- =====================================================================
-- 14 - Tipo do compromisso: delegacia, e o tipo escrito a mao
--
-- Duas mudancas na tabela AGENDA, as duas na mesma ideia: a lista fechada
-- de tipos nao dava conta do que o escritorio marca de verdade.
--
--   1. DELEGACIA entra na lista de tipos permitidos. Na tela ela toma o
--      lugar de "Ida ao DETRAN" nas opcoes oferecidas; DETRAN continua
--      aceito no banco para as marcacoes antigas seguirem validas.
--
--   2. TIPO_OUTRO guarda o que a pessoa escreve quando escolhe "Outro".
--      Fica a parte do TITULO de proposito: o titulo diz o que fazer, e
--      este diz que especie de compromisso e - e e ele que vira o selo.
--
-- Nao tem GRANT: a tabela AGENDA ja existe e o ESTACAO ja enxerga ela.
-- =====================================================================

-- ---------------------------------------------------------------------
-- 1. A lista de tipos aceitos
-- ---------------------------------------------------------------------
ALTER TABLE AGENDA DROP CONSTRAINT CK_AGENDA_TIPO;

ALTER TABLE AGENDA ADD CONSTRAINT CK_AGENDA_TIPO CHECK (TIPO IN (
    'PRAZO','ATENDIMENTO','PROVA','VISTORIA','DETRAN','DELEGACIA','TAREFA','OUTRO'));


-- ---------------------------------------------------------------------
-- 2. O tipo escrito a mao
-- ---------------------------------------------------------------------
ALTER TABLE AGENDA ADD TIPO_OUTRO VARCHAR2(40 CHAR);

COMMENT ON COLUMN AGENDA.TIPO_OUTRO IS
    'Que especie de compromisso e, escrito a mao, quando TIPO = OUTRO. Nulo nos demais.';


-- ---------------------------------------------------------------------
-- 3. Conferencia
-- ---------------------------------------------------------------------
SELECT SEARCH_CONDITION_VC AS REGRA_DO_TIPO
  FROM USER_CONSTRAINTS
 WHERE CONSTRAINT_NAME = 'CK_AGENDA_TIPO';

SELECT COLUMN_NAME, DATA_TYPE, DATA_LENGTH
  FROM USER_TAB_COLUMNS
 WHERE TABLE_NAME = 'AGENDA'
   AND COLUMN_NAME = 'TIPO_OUTRO';
