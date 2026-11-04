-- =====================================================================
-- GESTOR DE RECURSOS - anexos do cliente: o documento passa a morar no banco
-- =====================================================================
-- Rodar como GESTOR, nos DOIS bancos: primeiro no de teste, depois no de
-- producao. Enquanto nao rodar, a aba Anexos devolve erro de coluna
-- inexistente (ORA-00904).
--
-- POR QUE
--
-- A tabela ARQUIVOS nasceu com a coluna CAMINHO, pensada para guardar o
-- endereco de um arquivo que ficaria em outro lugar - no disco, ou num
-- servico de armazenamento. Essa ideia foi abandonada: o documento fica
-- dentro do Oracle, como BLOB.
--
-- O que decidiu foi o numero de copias. Com o arquivo fora do banco:
--
--   * sao dois backups a manter, nao um, e nenhum serve sozinho;
--   * sao duas credenciais e duas contas a renovar;
--   * a linha pode sobreviver ao arquivo, e a ficha passa a prometer um
--     documento que nao abre mais.
--
-- Com o arquivo dentro: um backup, uma credencial, e anexo que existe ou
-- nao existe - nunca pela metade.
--
-- O preco e espaco. A instancia Always Free tem 20 GB, UM SO, compartilhado
-- entre texto e documentos (nao sao 20 para cada). Por isso o teto de 10 MB
-- por arquivo entra como restricao do banco, nesta mesma mudanca.
--
-- O QUE MUDA
--
-- 1. A coluna CAMINHO sai.
-- 2. Entram CONTEUDO (BLOB, o documento) e TAMANHO (NUMBER, os bytes).
-- 3. Entra CK_ARQUIVOS_TAMANHO: de 1 byte a 10 MB.
-- 4. Os comentarios da tabela acompanham.
--
-- O TAMANHO repetido fora do BLOB nao e redundancia desperdicada: a lista
-- da ficha mostra o tamanho sem abrir o documento, e o backup semanal do
-- texto - que nao leva BLOB - ainda registra o que faltou levar.
--
-- SEGURANCA
--
-- O passo 2 ABORTA se a tabela tiver qualquer linha. Hoje ela esta vazia
-- nos dois bancos (conferido em 03/11/2026), e nada no sistema grava nela
-- ainda. Se um dia tiver dado, esta mudanca precisa de outro roteiro - o
-- DROP de CAMINHO apagaria o endereco dos anexos existentes.
--
-- COMO RODAR
--
-- No SQL Developer Web, "Executar script" (F5), nao "Executar instrucao".
-- =====================================================================

SET SERVEROUTPUT ON;

-- ---------------------------------------------------------------------
-- 1. Antes: como a tabela esta hoje
-- ---------------------------------------------------------------------
-- O esperado e CAMINHO presente, CONTEUDO ausente e zero linhas.

SELECT COLUMN_NAME, DATA_TYPE, CHAR_LENGTH, NULLABLE
  FROM USER_TAB_COLUMNS
 WHERE TABLE_NAME = 'ARQUIVOS'
 ORDER BY COLUMN_ID;

SELECT COUNT(*) AS LINHAS_HOJE FROM ARQUIVOS;


-- ---------------------------------------------------------------------
-- 2. A trava: nao mexer em tabela com dado
-- ---------------------------------------------------------------------

DECLARE
    v_linhas NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_linhas FROM ARQUIVOS;

    IF v_linhas > 0 THEN
        RAISE_APPLICATION_ERROR(-20001,
            'ARQUIVOS tem ' || v_linhas || ' linha(s). Este script apaga a coluna '
            || 'CAMINHO e so vale para tabela vazia. Nao continue.');
    END IF;

    DBMS_OUTPUT.PUT_LINE('Tabela vazia. Pode seguir.');
END;
/


-- ---------------------------------------------------------------------
-- 3. Fora a coluna de endereco
-- ---------------------------------------------------------------------
-- Dentro de bloco com conferencia para o script poder rodar duas vezes
-- sem erro - o que acontece quando alguem nao lembra se ja rodou.

DECLARE
    v_existe NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_existe
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'ARQUIVOS' AND COLUMN_NAME = 'CAMINHO';

    IF v_existe = 1 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE ARQUIVOS DROP COLUMN CAMINHO';
        DBMS_OUTPUT.PUT_LINE('CAMINHO removida.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('CAMINHO ja nao existia.');
    END IF;
END;
/


-- ---------------------------------------------------------------------
-- 4. Entram o documento e o tamanho
-- ---------------------------------------------------------------------

DECLARE
    v_existe NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_existe
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'ARQUIVOS' AND COLUMN_NAME = 'CONTEUDO';

    IF v_existe = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE ARQUIVOS ADD (CONTEUDO BLOB NOT NULL)';
        DBMS_OUTPUT.PUT_LINE('CONTEUDO criada.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('CONTEUDO ja existia.');
    END IF;

    SELECT COUNT(*) INTO v_existe
      FROM USER_TAB_COLUMNS
     WHERE TABLE_NAME = 'ARQUIVOS' AND COLUMN_NAME = 'TAMANHO';

    IF v_existe = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE ARQUIVOS ADD (TAMANHO NUMBER(10) NOT NULL)';
        DBMS_OUTPUT.PUT_LINE('TAMANHO criada.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('TAMANHO ja existia.');
    END IF;
END;
/


-- ---------------------------------------------------------------------
-- 5. O teto de 10 MB, no banco
-- ---------------------------------------------------------------------
-- Na tela tambem ha o limite, com mensagem legivel. Aqui e a ultima
-- palavra: a API e outra porta para a mesma tabela, e quem grava por la
-- nao passa pela tela.
--
-- 10485760 = 10 * 1024 * 1024. O minimo de 1 byte barra arquivo vazio,
-- que e o que sobra quando a leitura falha no meio.

DECLARE
    v_existe NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_existe
      FROM USER_CONSTRAINTS
     WHERE TABLE_NAME = 'ARQUIVOS' AND CONSTRAINT_NAME = 'CK_ARQUIVOS_TAMANHO';

    IF v_existe = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE ARQUIVOS ADD CONSTRAINT CK_ARQUIVOS_TAMANHO '
            || 'CHECK (TAMANHO BETWEEN 1 AND 10485760)';
        DBMS_OUTPUT.PUT_LINE('CK_ARQUIVOS_TAMANHO criada.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('CK_ARQUIVOS_TAMANHO ja existia.');
    END IF;
END;
/


-- ---------------------------------------------------------------------
-- 6. Os comentarios
-- ---------------------------------------------------------------------

COMMENT ON TABLE ARQUIVOS IS 'Anexos do cliente. O documento fica aqui dentro, em CONTEUDO - nao em servico de fora.';

COMMENT ON COLUMN ARQUIVOS.CONTEUDO IS 'O documento, em PDF. Nao vem no backup semanal do texto: sai pela exportacao em massa.';

COMMENT ON COLUMN ARQUIVOS.TAMANHO IS 'Bytes do documento. Repetido fora do BLOB para a lista nao precisar abri-lo.';

COMMENT ON COLUMN ARQUIVOS.INSTANCIA_ID IS 'A fase a que o documento pertence. Nulo quando e do cliente, e nao de um recurso.';


-- ---------------------------------------------------------------------
-- 7. Depois: conferencia
-- ---------------------------------------------------------------------
-- CONTEUDO (BLOB) e TAMANHO (NUMBER) precisam aparecer, CAMINHO nao, e
-- CK_ARQUIVOS_TAMANHO precisa estar ENABLED/VALIDATED.

SELECT COLUMN_NAME, DATA_TYPE, CHAR_LENGTH, NULLABLE
  FROM USER_TAB_COLUMNS
 WHERE TABLE_NAME = 'ARQUIVOS'
 ORDER BY COLUMN_ID;

SELECT CONSTRAINT_NAME, CONSTRAINT_TYPE, STATUS, VALIDATED, SEARCH_CONDITION
  FROM USER_CONSTRAINTS
 WHERE TABLE_NAME = 'ARQUIVOS'
   AND CONSTRAINT_NAME NOT LIKE 'SYS_%'
 ORDER BY CONSTRAINT_TYPE, CONSTRAINT_NAME;
