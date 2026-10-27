-- =====================================================================
-- GESTOR DE RECURSOS - resultado "ANULADO" nas instancias
-- =====================================================================
-- Rodar como GESTOR, nos DOIS bancos: primeiro no de teste, depois no de
-- producao. Enquanto nao rodar, escolher "Anulado" na tela devolve erro de
-- constraint (ORA-02290).
--
-- POR QUE
--
-- Ha casos em que a fase cai sem ser julgada e o processo segue direto na
-- instancia seguinte - a autuacao e anulada e o caso vai para a JARI sem
-- ter passado por ela.
--
-- Isso nao e deferido, que encerraria o recurso, nem indeferido, que
-- pressupoe julgamento. E um terceiro desfecho, que abre a proxima fase
-- como o indeferido mas nao exige protocolo nem data.
--
-- O QUE MUDA
--
-- Dois CHECK: o do RESULTADO ganha 'ANULADO', e o da SITUACAO tambem,
-- porque a situacao da instancia e deduzida do resultado.
--
-- Nenhuma linha existente e alterada. Nenhuma coluna e criada. O script
-- so afrouxa as duas restricoes, entao e seguro rodar com o sistema em
-- uso.
--
-- COMO RODAR
--
-- No SQL Developer Web, "Executar script" (F5), nao "Executar instrucao".
-- =====================================================================

SET SERVEROUTPUT ON;

-- ---------------------------------------------------------------------
-- 1. Antes: como estao as restricoes hoje
-- ---------------------------------------------------------------------

SELECT CONSTRAINT_NAME, SEARCH_CONDITION_VC
  FROM USER_CONSTRAINTS
 WHERE TABLE_NAME = 'INSTANCIAS'
   AND CONSTRAINT_NAME IN ('CK_INSTANCIAS_RESULT', 'CK_INSTANCIAS_SITUACAO');


-- ---------------------------------------------------------------------
-- 2. Trocar as duas restricoes
-- ---------------------------------------------------------------------
-- Oracle nao altera um CHECK no lugar: derruba e recria.

ALTER TABLE INSTANCIAS DROP CONSTRAINT CK_INSTANCIAS_RESULT;

ALTER TABLE INSTANCIAS ADD CONSTRAINT CK_INSTANCIAS_RESULT
    CHECK (RESULTADO IN ('DEFERIDO','INDEFERIDO','ANULADO'));

ALTER TABLE INSTANCIAS DROP CONSTRAINT CK_INSTANCIAS_SITUACAO;

ALTER TABLE INSTANCIAS ADD CONSTRAINT CK_INSTANCIAS_SITUACAO
    CHECK (SITUACAO IN (
        'CONTRATADO',
        'AGUARDANDO_DOCUMENTO',
        'AGUARDANDO_PROTOCOLO_JARI',
        'AGUARDANDO_PROTOCOLO_CETRAN',
        'PROTOCOLADO',
        'AGUARDANDO_JULGAMENTO',
        'DEFERIDO',
        'INDEFERIDO',
        'CANCELADO',
        'DESISTENCIA',
        'PRAZO_PERDIDO',
        'ANULADO'));


-- ---------------------------------------------------------------------
-- 3. Depois: conferencia
-- ---------------------------------------------------------------------
-- As duas precisam aparecer com ANULADO dentro, e ENABLED/VALIDATED.

SELECT CONSTRAINT_NAME, STATUS, VALIDATED, SEARCH_CONDITION_VC
  FROM USER_CONSTRAINTS
 WHERE TABLE_NAME = 'INSTANCIAS'
   AND CONSTRAINT_NAME IN ('CK_INSTANCIAS_RESULT', 'CK_INSTANCIAS_SITUACAO');
