-- =====================================================================
-- GESTOR DE RECURSOS - apagar uma contratacao inteira (SO DADO DE TESTE)
-- =====================================================================
-- O sistema nao oferece isto de proposito: contratacao e dinheiro, e
-- dinheiro nao se apaga, se cancela (botao "Encerrar recurso", na aba
-- Processos). Este script existe para limpar o que foi criado em teste.
--
-- ANTES DE RODAR, confira na primeira consulta que a contratacao e de um
-- cliente "TESTE ...". Nao use em contratacao de cliente real.
--
-- Como usar: troque o numero nas quatro linhas abaixo pelo ID da
-- contratacao e execute o arquivo inteiro.
-- =====================================================================

-- 1. De quem e, e o que vai junto. Olhe antes de apagar.
SELECT CT.ID            AS CONTRATACAO,
       C.NOME           AS CLIENTE,
       CT.VALOR_FINAL,
       CT.SITUACAO,
       (SELECT COUNT(*) FROM ITENS_CONTRATACAO I WHERE I.CONTRATACAO_ID = CT.ID) AS ITENS,
       (SELECT COUNT(*) FROM PAGAMENTOS P       WHERE P.CONTRATACAO_ID = CT.ID) AS PAGAMENTOS
  FROM CONTRATACOES CT
  JOIN CLIENTES C ON C.ID = CT.CLIENTE_ID
 WHERE CT.ID = 0;   -- <<< ID da contratacao


-- 2. O dinheiro sai primeiro: as chaves de PAGAMENTOS e DESPESAS nao
--    apagam em cascata, e e bom que nao apaguem mesmo.
DELETE FROM PAGAMENTOS WHERE CONTRATACAO_ID = 0;   -- <<< ID da contratacao
DELETE FROM DESPESAS   WHERE CONTRATACAO_ID = 0;   -- <<< ID da contratacao


-- 3. A contratacao leva junto os itens, e os itens levam as instancias
--    e as etapas: essas chaves sao ON DELETE CASCADE.
DELETE FROM CONTRATACOES WHERE ID = 0;             -- <<< ID da contratacao

COMMIT;


-- 4. Conferencia: tem que voltar vazio.
SELECT ID FROM CONTRATACOES WHERE ID = 0;          -- <<< ID da contratacao
