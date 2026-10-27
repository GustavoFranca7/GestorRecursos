-- =====================================================================
-- GESTOR DE RECURSOS - login e recuperacao de senha
-- =====================================================================
-- Ate aqui o sistema abria direto. A tabela USUARIOS ja existia desde o
-- 01-CriacaoBanco, com LOGIN e SENHA_HASH, mas nunca foi usada.
--
-- COMO O ACESSO NASCE (decidido em 23/10)
--
-- O acesso e criado AQUI, no banco, pela mao de quem administra. A tela
-- de login nao cadastra ninguem: tela que cria usuario sozinha deixa
-- qualquer um entrar.
--
-- A senha inicial e escolhida por quem cria, e portanto conhecida por
-- ele. Por isso ela nao serve como senha definitiva: no primeiro acesso
-- o sistema abre, e por cima dele aparece uma janela que nao fecha,
-- pedindo senha nova e e-mail de recuperacao. Enquanto isso nao for
-- feito, nao da para usar o sistema.
--
-- O gatilho e o EMAIL_RECUPERACAO vazio. Quem ja tem e-mail gravado nao
-- ve a janela - e e assim que se cria um acesso de administrador ja
-- pronto, preenchendo o e-mail no proprio INSERT.
--
-- QUEM CRIA OS OUTROS (decidido em 23/10)
--
-- So o administrador. Em Configuracoes existe uma aba Usuarios que nao
-- aparece para os demais: de la os acessos sao criados pelo proprio
-- sistema, que calcula o hash sozinho. O --hash abaixo serve uma vez
-- so, para o primeiro administrador - depois dele, nunca mais.
--
-- Isto refina o RNF-01 ("ambos com acesso total"): administrar usuarios
-- e do administrador. Ver e lancar continua igual para os dois.
--
-- O QUE AS COLUNAS FAZEM
--
--   EMAIL_RECUPERACAO  para onde vai o codigo. Vazio = primeiro acesso
--                      ainda nao feito, e a janela aparece.
--
--   CODIGO_HASH        o codigo de 6 digitos, guardado em hash pelo
--                      mesmo motivo da senha: quem le o banco nao pode
--                      entrar com o que leu.
--
--   CODIGO_EXPIRA_EM   codigo que nao vence vira senha permanente.
--
--   CODIGO_TENTATIVAS  seis digitos sao um milhao de combinacoes, que um
--                      programa testa em minutos. Tres erros queimam o
--                      codigo e obrigam a pedir outro.
--
-- Rode a parte 1 inteira. Ela e segura: so acrescenta colunas e nao toca
-- em nenhum dado existente.
-- =====================================================================

-- =====================================================================
-- 1. As colunas
-- =====================================================================

ALTER TABLE USUARIOS ADD (
    EMAIL_RECUPERACAO  VARCHAR2(120 CHAR),
    CODIGO_HASH        VARCHAR2(200 CHAR),
    CODIGO_EXPIRA_EM   DATE,
    CODIGO_TENTATIVAS  NUMBER(2) DEFAULT 0 NOT NULL,
    ADMINISTRADOR      CHAR(1) DEFAULT 'N' NOT NULL
);

ALTER TABLE USUARIOS ADD CONSTRAINT CK_USUARIOS_ADM
    CHECK (ADMINISTRADOR IN ('S','N'));

COMMENT ON COLUMN USUARIOS.EMAIL_RECUPERACAO IS
    'Para onde vai o codigo de recuperacao. Vazio = primeiro acesso ainda nao feito.';

COMMENT ON COLUMN USUARIOS.CODIGO_HASH IS
    'Hash do codigo de recuperacao. Codigo em texto puro nunca entra aqui.';

COMMENT ON COLUMN USUARIOS.CODIGO_EXPIRA_EM IS
    'Quando o codigo deixa de valer. Fora da janela, nao serve mais.';

COMMENT ON COLUMN USUARIOS.CODIGO_TENTATIVAS IS
    'Erros seguidos no codigo atual. Em tres, o codigo e queimado.';

COMMENT ON COLUMN USUARIOS.ADMINISTRADOR IS
    'S ve a aba Usuarios em Configuracoes e cria acessos. N usa o sistema normalmente.';


-- Quem pede codigo informa o login; o indice evita varrer a tabela, e
-- mais ainda quando ela tiver mais gente que as duas de hoje.
CREATE INDEX IX_USUARIOS_EMAIL ON USUARIOS (EMAIL_RECUPERACAO);


-- =====================================================================
-- 2. Criar os acessos
-- =====================================================================
-- SENHA_HASH nao se escreve a mao: e PBKDF2, que o SQL nao calcula.
-- Para obter o valor, rode o programa com --hash:
--
--     GestorRecursos.Desktop.exe --hash
--
-- Abre uma janelinha: digite a senha inicial, clique em Gerar e copie o
-- resultado. Cole no lugar de COLE_O_HASH_AQUI.
--
-- Aqui se cria UM usuario so: o administrador. Os outros saem da aba
-- Usuarios, dentro de Configuracoes, sem precisar de hash nenhum.
--
-- Nao deixe este arquivo com hash de verdade dentro: o repositorio e
-- publico.
-- =====================================================================

-- O administrador. Ja vai com e-mail para nao cair na janela de
-- primeiro acesso, e com ADMINISTRADOR = 'S' para enxergar a aba
-- Usuarios. E o unico INSERT feito a mao neste projeto.
--
-- INSERT INTO USUARIOS (NOME, LOGIN, SENHA_HASH, ATIVO, EMAIL_RECUPERACAO, ADMINISTRADOR)
-- VALUES ('Gustavo Franca', 'adm', 'COLE_O_HASH_AQUI', 'S', 'seu@email.com', 'S');

-- COMMIT;

-- O acesso dela sai da tela: entre como administrador, abra
-- Configuracoes > Usuarios e crie por la.


-- =====================================================================
-- Conferencia
-- =====================================================================
SELECT ID, NOME, LOGIN, ATIVO, ADMINISTRADOR, EMAIL_RECUPERACAO
  FROM USUARIOS
 ORDER BY ID;

-- EMAIL_RECUPERACAO vazio = vai cair na janela de primeiro acesso.
-- Preenchido = entra direto.
