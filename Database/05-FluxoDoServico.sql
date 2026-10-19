-- =====================================================================
-- GESTOR DE RECURSOS - o fluxo de cada servico
-- =====================================================================
-- A tela "+ Novo servico" precisa saber, antes de perguntar qualquer
-- coisa, o que o servico escolhido exige: um veiculo e multas, um
-- processo do DETRAN, so o veiculo, ou nada alem do cliente.
--
-- TEM_INSTANCIAS nao responde isso: ela diz como o servico e
-- ACOMPANHADO (tres fases ou checklist), nao o que ele PEDE.
-- Licenciamento precisa de veiculo e nao tem instancias; indicacao de
-- condutor precisa de multa e tambem nao tem.
--
-- Deduzir pelo nome seria frustrar o dia em que um servico novo for
-- cadastrado pela tela de Servicos e precos.
--
-- Rode este arquivo inteiro. Ele e seguro: so acrescenta uma coluna e
-- preenche os 18 servicos que ja existem.
-- =====================================================================

ALTER TABLE TIPOS_SERVICO ADD (
    FLUXO VARCHAR2(14 CHAR) DEFAULT 'OUTRO' NOT NULL
);

ALTER TABLE TIPOS_SERVICO ADD CONSTRAINT CK_TIPOS_SERVICO_FLUXO
    CHECK (FLUXO IN ('RECURSO_MULTA','INDICACAO','DEFESA_CNH','SERVICO_CNH','VEICULO','OUTRO'));

COMMENT ON COLUMN TIPOS_SERVICO.FLUXO IS
    'O que o servico pede no cadastro: RECURSO_MULTA e INDICACAO pedem veiculo e multa; DEFESA_CNH pede o processo do DETRAN; VEICULO pede so o carro; SERVICO_CNH e OUTRO nao pedem nada alem do cliente.';


-- ---------------------------------------------------------------------
-- Os 18 servicos da carga inicial
-- ---------------------------------------------------------------------

-- Pedem veiculo e multa. Os seis recursos aparecem como uma opcao so na
-- tela: qual deles e o certo depende da gravidade, que so se sabe depois
-- de escolher a multa.
UPDATE TIPOS_SERVICO SET FLUXO = 'RECURSO_MULTA'
 WHERE NOME IN ('Recurso de multa leve',
                'Recurso de multa media',
                'Recurso de multa grave',
                'Recurso de multa gravissima',
                'Recurso de multa gravissima 3x',
                'Recurso de multa gravissima suspensiva');

UPDATE TIPOS_SERVICO SET FLUXO = 'INDICACAO'
 WHERE NOME = 'Indicacao de condutor';

-- Pedem o processo que o DETRAN abriu, e as multas que contaram na
-- pontuacao. Com ou sem indicacao e preco, nao caminho: tambem viram
-- uma opcao so na tela.
UPDATE TIPOS_SERVICO SET FLUXO = 'DEFESA_CNH'
 WHERE NOME IN ('Suspensao do direito de dirigir com indicacao',
                'Suspensao do direito de dirigir sem indicacao',
                'Cassacao do direito de dirigir com indicacao',
                'Cassacao do direito de dirigir sem indicacao');

-- Nao pedem nada alem do cliente, mas sao assunto de CNH: ficam juntos
-- na lista porque e assim que ela procura.
UPDATE TIPOS_SERVICO SET FLUXO = 'SERVICO_CNH'
 WHERE NOME IN ('Reciclagem basica',
                'Reciclagem completa',
                'Renovacao de CNH',
                'Consulta de CNH');

UPDATE TIPOS_SERVICO SET FLUXO = 'VEICULO'
 WHERE NOME = 'Licenciamento do veiculo';

UPDATE TIPOS_SERVICO SET FLUXO = 'OUTRO'
 WHERE NOME IN ('Cadastro de Uber', 'Passaporte');

COMMIT;


-- ---------------------------------------------------------------------
-- Conferencia: nenhuma linha pode sobrar sem fluxo pensado.
-- O esperado e 6 RECURSO_MULTA, 4 DEFESA_CNH, 4 SERVICO_CNH,
-- 2 OUTRO, 1 INDICACAO e 1 VEICULO.
-- ---------------------------------------------------------------------
SELECT FLUXO, COUNT(*) AS QUANTOS
  FROM TIPOS_SERVICO
 GROUP BY FLUXO
 ORDER BY FLUXO;
