-- =====================================================================
-- GESTOR DE RECURSOS - prazo das etapas do servico direto
-- =====================================================================
-- O roteiro de um servico direto tinha data de conclusao, mas nao tinha
-- prazo. Sem prazo nao ha alarme: "agendar a pericia medica" podia ficar
-- parado por semanas sem ninguem perceber, e e justamente esse tipo de
-- etapa que vence.
--
-- A data de conclusao continua sendo preenchida sozinha quando a caixa
-- e marcada; as duas passam a ser editaveis pela tela.
--
-- Rode este arquivo inteiro. Ele so acrescenta uma coluna.
-- =====================================================================

ALTER TABLE ETAPAS ADD (
    PRAZO DATE
);

COMMENT ON COLUMN ETAPAS.PRAZO IS
    'Ate quando esta etapa precisa estar cumprida. Alimenta o alarme e, no futuro, a agenda.';


-- ---------------------------------------------------------------------
-- Conferencia: a coluna tem que aparecer, e vazia.
-- ---------------------------------------------------------------------
SELECT COUNT(*) AS ETAPAS, COUNT(PRAZO) AS COM_PRAZO FROM ETAPAS;
