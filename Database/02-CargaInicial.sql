-- =====================================================================
-- GESTOR DE RECURSOS - carga inicial
-- =====================================================================
-- Rode depois do 01-CriacaoBanco.sql.
--
-- Contem so dado de catalogo: tabela de precos e roteiros dos servicos.
-- Nada de dado pessoal aqui, porque este arquivo vai para o GitHub.
-- Os dados do escritorio ficam no 03, que nao e versionado.
-- =====================================================================


-- ---------------------------------------------------------------------
-- Tabela de precos (valores informados pela cliente)
-- TEM_INSTANCIAS = 'S' para o que percorre autuacao, JARI e CETRAN.
-- ---------------------------------------------------------------------

INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Indicacao de condutor',                     'N',   50.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Recurso de multa leve',                    'S',   40.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Recurso de multa media',                   'S',   65.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Recurso de multa grave',                   'S',  100.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Recurso de multa gravissima',              'S',  150.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Recurso de multa gravissima 3x',           'S',  300.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Recurso de multa gravissima suspensiva',   'S',  600.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Suspensao do direito de dirigir com indicacao',    'S',  700.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Suspensao do direito de dirigir sem indicacao',    'S', 1200.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Cassacao do direito de dirigir com indicacao',     'S',  700.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Cassacao do direito de dirigir sem indicacao',     'S', 1200.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Reciclagem basica',                        'N',  500.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Reciclagem completa',                      'N',  700.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Renovacao de CNH',                         'N',  270.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Cadastro de Uber',                         'N',  150.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Passaporte',                               'N',  150.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Licenciamento do veiculo',                 'N',    0.00);
INSERT INTO TIPOS_SERVICO (NOME, TEM_INSTANCIAS, VALOR_TABELA) VALUES ('Consulta de CNH',                          'N',    0.00);


-- ---------------------------------------------------------------------
-- Roteiro dos servicos diretos, como ela descreveu (Q32).
-- A subconsulta busca o ID pelo nome para o script nao depender de o
-- identity ter comecado em 1.
-- ---------------------------------------------------------------------

-- Reciclagem basica
INSERT INTO ETAPAS_MODELO (TIPO_SERVICO_ID, ORDEM, NOME)
    SELECT ID, 1, 'Concluir o curso'   FROM TIPOS_SERVICO WHERE NOME = 'Reciclagem basica';
INSERT INTO ETAPAS_MODELO (TIPO_SERVICO_ID, ORDEM, NOME)
    SELECT ID, 2, 'Pagar a taxa'       FROM TIPOS_SERVICO WHERE NOME = 'Reciclagem basica';
INSERT INTO ETAPAS_MODELO (TIPO_SERVICO_ID, ORDEM, NOME)
    SELECT ID, 3, 'Agendar a prova'    FROM TIPOS_SERVICO WHERE NOME = 'Reciclagem basica';

-- Reciclagem completa
INSERT INTO ETAPAS_MODELO (TIPO_SERVICO_ID, ORDEM, NOME)
    SELECT ID, 1, 'Concluir o curso'   FROM TIPOS_SERVICO WHERE NOME = 'Reciclagem completa';
INSERT INTO ETAPAS_MODELO (TIPO_SERVICO_ID, ORDEM, NOME)
    SELECT ID, 2, 'Pagar a taxa'       FROM TIPOS_SERVICO WHERE NOME = 'Reciclagem completa';
INSERT INTO ETAPAS_MODELO (TIPO_SERVICO_ID, ORDEM, NOME)
    SELECT ID, 3, 'Agendar a prova'    FROM TIPOS_SERVICO WHERE NOME = 'Reciclagem completa';

-- Renovacao de CNH
INSERT INTO ETAPAS_MODELO (TIPO_SERVICO_ID, ORDEM, NOME)
    SELECT ID, 1, 'Acessar o gov.br do cliente'  FROM TIPOS_SERVICO WHERE NOME = 'Renovacao de CNH';
INSERT INTO ETAPAS_MODELO (TIPO_SERVICO_ID, ORDEM, NOME)
    SELECT ID, 2, 'Pagar a taxa'                 FROM TIPOS_SERVICO WHERE NOME = 'Renovacao de CNH';
INSERT INTO ETAPAS_MODELO (TIPO_SERVICO_ID, ORDEM, NOME)
    SELECT ID, 3, 'Agendar a foto'               FROM TIPOS_SERVICO WHERE NOME = 'Renovacao de CNH';
INSERT INTO ETAPAS_MODELO (TIPO_SERVICO_ID, ORDEM, NOME)
    SELECT ID, 4, 'Agendar o exame'              FROM TIPOS_SERVICO WHERE NOME = 'Renovacao de CNH';
INSERT INTO ETAPAS_MODELO (TIPO_SERVICO_ID, ORDEM, NOME)
    SELECT ID, 5, 'Retirar o documento'          FROM TIPOS_SERVICO WHERE NOME = 'Renovacao de CNH';

-- Cadastro de Uber
INSERT INTO ETAPAS_MODELO (TIPO_SERVICO_ID, ORDEM, NOME)
    SELECT ID, 1, 'Colher a documentacao'        FROM TIPOS_SERVICO WHERE NOME = 'Cadastro de Uber';
INSERT INTO ETAPAS_MODELO (TIPO_SERVICO_ID, ORDEM, NOME)
    SELECT ID, 2, 'Acompanhar atualizacoes'      FROM TIPOS_SERVICO WHERE NOME = 'Cadastro de Uber';
INSERT INTO ETAPAS_MODELO (TIPO_SERVICO_ID, ORDEM, NOME)
    SELECT ID, 3, 'Informar data da vistoria'    FROM TIPOS_SERVICO WHERE NOME = 'Cadastro de Uber';
INSERT INTO ETAPAS_MODELO (TIPO_SERVICO_ID, ORDEM, NOME)
    SELECT ID, 4, 'Retirar o cartao'             FROM TIPOS_SERVICO WHERE NOME = 'Cadastro de Uber';

COMMIT;


-- ---------------------------------------------------------------------
-- Conferencia rapida: 18 servicos e 15 etapas.
-- ---------------------------------------------------------------------
SELECT (SELECT COUNT(*) FROM TIPOS_SERVICO)  AS SERVICOS,
       (SELECT COUNT(*) FROM ETAPAS_MODELO)  AS ETAPAS
  FROM DUAL;
