-- =====================================================================
-- GESTOR DE RECURSOS - pagar com o saldo do cliente
-- =====================================================================
-- Rodar como GESTOR, nos DOIS bancos: primeiro no de teste, depois no de
-- producao. Enquanto nao rodar, escolher "Saldo" na tela devolve erro de
-- constraint (ORA-02290) e nada e gravado.
--
-- POR QUE
--
-- Quando um dinheiro volta para o cliente - servico cancelado, cobranca a
-- mais -, ele fica como saldo na tabela CREDITOS. Ate agora esse saldo era
-- so um lembrete: a despachante via o valor e dava desconto na negociacao,
-- a mao.
--
-- Agora o saldo pode quitar uma contratacao direto, e para isso ele
-- precisa virar um PAGAMENTO - e o que faz o EM_ABERTO da contratacao
-- diminuir, porque o saldo em aberto e VALOR_FINAL menos a soma dos
-- pagamentos.
--
-- O QUE MUDA
--
-- Um CHECK: o da FORMA ganha 'SALDO', ao lado das cinco que ja existiam.
-- Atencao para nao confundir com 'CREDITO', que e cartao de credito.
--
-- Nenhuma linha existente e alterada. Nenhuma coluna e criada. O script
-- so afrouxa a restricao, entao e seguro rodar com o sistema em uso.
--
-- O saldo usado NAO conta como dinheiro que entrou no mes: ele entrou
-- quando virou saldo, e contar de novo infla o caixa. Quem trata disso e
-- a tela, nao o banco - aqui o pagamento e um pagamento como os outros.
--
-- COMO RODAR
--
-- No SQL Developer Web, "Executar script" (F5), nao "Executar instrucao".
-- =====================================================================


-- ---------------------------------------------------------------------
-- 1. A forma de pagamento nova
-- ---------------------------------------------------------------------

ALTER TABLE PAGAMENTOS DROP CONSTRAINT CK_PAGAMENTOS_FORMA;

ALTER TABLE PAGAMENTOS ADD CONSTRAINT CK_PAGAMENTOS_FORMA CHECK (FORMA IN (
    'DINHEIRO','PIX','PIX_PARCELADO','DEBITO','CREDITO','SALDO'));

COMMENT ON COLUMN PAGAMENTOS.FORMA IS
    'DINHEIRO, PIX, PIX_PARCELADO, DEBITO, CREDITO (cartao) e SALDO (credito do proprio cliente).';


-- ---------------------------------------------------------------------
-- 2. Conferencia
-- ---------------------------------------------------------------------
-- Tem que listar a constraint com 'SALDO' dentro.

SELECT CONSTRAINT_NAME, SEARCH_CONDITION_VC
  FROM USER_CONSTRAINTS
 WHERE TABLE_NAME = 'PAGAMENTOS'
   AND CONSTRAINT_NAME = 'CK_PAGAMENTOS_FORMA';
