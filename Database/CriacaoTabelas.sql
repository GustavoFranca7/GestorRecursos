-- 1. CRIANDO A TABELA DE CLIENTES
CREATE TABLE CLIENTES (
    ID            NUMBER(10)          PRIMARY KEY,
    NOME          VARCHAR2(100)       NOT NULL,
    CPF           VARCHAR2(11)        NOT NULL UNIQUE,
    EMAIL         VARCHAR2(100),
    TELEFONE      VARCHAR2(15)        NOT NULL,
    DATA_CADASTRO DATE              DEFAULT SYSDATE NOT NULL
);

-- Sequence para autoincrementar o ID do Cliente
CREATE SEQUENCE SEQ_CLIENTES START WITH 1 INCREMENT BY 1;


-- 2. CRIANDO A TABELA DE ENDEREÇOS (Relacionada ao Cliente)
CREATE TABLE ENDERECOS (
    ID          NUMBER(10)          PRIMARY KEY,
    CLIENTE_ID  NUMBER(10)          NOT NULL,
    LOGRADOURO  VARCHAR2(150)       NOT NULL,
    NUMERO      VARCHAR2(20)        NOT NULL,
    CIDADE      VARCHAR2(50)        NOT NULL,
    ESTADO      VARCHAR2(2)         NOT NULL,
    CEP         VARCHAR2(8)         NOT NULL,
    CONSTRAINT FK_ENDERECO_CLIENTE FOREIGN KEY (CLIENTE_ID) REFERENCES CLIENTES(ID)
);

-- Sequence para autoincrementar o ID do Endereço
CREATE SEQUENCE SEQ_ENDERECOS START WITH 1 INCREMENT BY 1;

-- Índice na coluna de chave estrangeira: evita bloqueio de tabela                                                 
-- no DELETE do cliente e agiliza as buscas de endereço por cliente                                               
CREATE INDEX IDX_ENDERECOS_CLIENTE_ID ON ENDERECOS(CLIENTE_ID);  