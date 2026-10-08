-- =====================================================================
-- GESTOR DE RECURSOS - limpeza (SO EM DESENVOLVIMENTO)
-- =====================================================================
-- APAGA TODAS AS TABELAS DO SISTEMA E TUDO QUE ESTIVER DENTRO DELAS.
--
-- Existe para recriar o banco do zero enquanto o modelo ainda muda.
-- No dia em que houver dado real, este arquivo nao deve mais ser usado:
-- a partir dali, mudanca de estrutura se faz com ALTER TABLE.
--
-- A ordem e a inversa da criacao, para nao esbarrar em chave estrangeira.
-- =====================================================================

DROP VIEW VW_COBRANCAS_ABERTAS;
DROP VIEW VW_CONTRATACAO_FINANCEIRO;

DROP TABLE DIARIO_DIA         CASCADE CONSTRAINTS;
DROP TABLE AGENDA             CASCADE CONSTRAINTS;
DROP TABLE ARQUIVOS           CASCADE CONSTRAINTS;
DROP TABLE CREDITOS           CASCADE CONSTRAINTS;
DROP TABLE DESPESAS           CASCADE CONSTRAINTS;
DROP TABLE PAGAMENTOS         CASCADE CONSTRAINTS;
DROP TABLE ETAPAS             CASCADE CONSTRAINTS;
DROP TABLE INSTANCIAS         CASCADE CONSTRAINTS;
DROP TABLE ITENS_CONTRATACAO  CASCADE CONSTRAINTS;
DROP TABLE CONTRATACOES       CASCADE CONSTRAINTS;
DROP TABLE ETAPAS_MODELO      CASCADE CONSTRAINTS;
DROP TABLE TIPOS_SERVICO      CASCADE CONSTRAINTS;
DROP TABLE PROCESSO_MULTA     CASCADE CONSTRAINTS;
DROP TABLE PROCESSOS_CNH      CASCADE CONSTRAINTS;
DROP TABLE MULTAS             CASCADE CONSTRAINTS;
DROP TABLE CLIENTE_VEICULO    CASCADE CONSTRAINTS;
DROP TABLE VEICULOS           CASCADE CONSTRAINTS;
DROP TABLE ENDERECOS          CASCADE CONSTRAINTS;
DROP TABLE CLIENTES           CASCADE CONSTRAINTS;
DROP TABLE ESCRITORIO         CASCADE CONSTRAINTS;
DROP TABLE USUARIOS           CASCADE CONSTRAINTS;

-- A lixeira do Oracle guarda o que foi apagado e o nome volta a aparecer
-- em consultas de catalogo. Esvaziar evita confusao na hora de recriar.
PURGE RECYCLEBIN;
