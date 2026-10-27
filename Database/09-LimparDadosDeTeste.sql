-- =====================================================================
-- GESTOR DE RECURSOS - limpar os dados de teste
-- =====================================================================
-- Rodar UMA vez, no banco de PRODUCAO, conectado como GESTOR, antes de
-- entregar o sistema.
--
-- Os dados de teste nasceram no banco real porque, ate 27/10/2026, nao
-- havia outro. Com o ambiente de teste de pe, eles saem daqui.
--
-- NAO rode no banco de teste: la eles sao o conteudo.
--
-- O QUE SAI
--
-- A convencao combinada: NOME comecando em "TESTE ", CPF em 900000000,
-- placas e AIT em TST. Nada fora desse padrao e tocado.
--
-- Uma excecao importante: as multas saem pelo VEICULO, e nao pelo
-- prefixo do AIT. Multa cadastrada a mao durante os testes tem AIT de
-- verdade num veiculo de teste; apagando so por AIT, ela ficaria para
-- tras e o DELETE do veiculo pararia na chave estrangeira.
--
-- COMO RODAR
--
-- No SQL Developer Web, use "Executar script" (F5), nao "Executar
-- instrucao". Confira os numeros da parte 1 antes de seguir.
-- =====================================================================

SET SERVEROUTPUT ON;

-- ---------------------------------------------------------------------
-- 1. Antes: o que ha no banco
-- ---------------------------------------------------------------------
-- A coluna "real" precisa bater com o que ela ja cadastrou. Se houver
-- cliente real aqui, pare e confira antes de continuar.

SELECT 'clientes'      AS tabela,
       COUNT(CASE WHEN CPF LIKE '900000000%' THEN 1 END)     AS teste,
       COUNT(CASE WHEN CPF NOT LIKE '900000000%' THEN 1 END) AS real_
  FROM CLIENTES
UNION ALL
SELECT 'veiculos',
       COUNT(CASE WHEN PLACA LIKE 'TST%' THEN 1 END),
       COUNT(CASE WHEN PLACA NOT LIKE 'TST%' THEN 1 END)
  FROM VEICULOS;


-- ---------------------------------------------------------------------
-- 2. Apagar
-- ---------------------------------------------------------------------
-- De dentro para fora: o que aponta sai antes do que e apontado.

DELETE FROM AGENDA
 WHERE CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%');

DELETE FROM ARQUIVOS
 WHERE CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%');

DELETE FROM PAGAMENTOS
 WHERE CONTRATACAO_ID IN (SELECT ID FROM CONTRATACOES
                           WHERE CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%'));

DELETE FROM DESPESAS
 WHERE CONTRATACAO_ID IN (SELECT ID FROM CONTRATACOES
                           WHERE CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%'));

DELETE FROM CREDITOS
 WHERE CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%');

DELETE FROM ETAPAS
 WHERE ITEM_ID IN (SELECT I.ID FROM ITENS_CONTRATACAO I
                     JOIN CONTRATACOES C ON C.ID = I.CONTRATACAO_ID
                    WHERE C.CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%'));

DELETE FROM INSTANCIAS
 WHERE ITEM_ID IN (SELECT I.ID FROM ITENS_CONTRATACAO I
                     JOIN CONTRATACOES C ON C.ID = I.CONTRATACAO_ID
                    WHERE C.CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%'));

DELETE FROM ITENS_CONTRATACAO
 WHERE CONTRATACAO_ID IN (SELECT ID FROM CONTRATACOES
                           WHERE CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%'));

DELETE FROM CONTRATACOES
 WHERE CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%');

DELETE FROM PROCESSO_MULTA
 WHERE PROCESSO_CNH_ID IN (SELECT ID FROM PROCESSOS_CNH
                            WHERE CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%'));

DELETE FROM PROCESSOS_CNH
 WHERE CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%');

-- Pelo veiculo, e nao pelo AIT: veja a observacao do cabecalho.
DELETE FROM MULTAS
 WHERE VEICULO_ID IN (SELECT ID FROM VEICULOS WHERE PLACA LIKE 'TST%');

DELETE FROM CLIENTE_VEICULO
 WHERE VEICULO_ID IN (SELECT ID FROM VEICULOS WHERE PLACA LIKE 'TST%')
    OR CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%');

DELETE FROM VEICULOS WHERE PLACA LIKE 'TST%';

DELETE FROM ENDERECOS
 WHERE CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%');

DELETE FROM CLIENTES WHERE CPF LIKE '900000000%';


-- ---------------------------------------------------------------------
-- 3. Os acessos de teste
-- ---------------------------------------------------------------------
-- Apaga os acessos criados para experimentar o login. O "adm" e o da
-- despachante ficam.
--
-- O "Claude" tambem sai: ele e administrador e a senha dele e conhecida.
-- Os testes passaram a rodar no banco de teste, onde ele existe de novo.

DELETE FROM USUARIOS
 WHERE LOWER(LOGIN) IN ('teste', 'teste2', 'claude');


COMMIT;


-- ---------------------------------------------------------------------
-- 4. Depois: conferencia
-- ---------------------------------------------------------------------
-- Tudo deve dar zero, menos os acessos que ficaram.

SELECT 'clientes de teste' AS o_que, COUNT(*) AS quantos FROM CLIENTES  WHERE CPF LIKE '900000000%'
UNION ALL
SELECT 'veiculos de teste',          COUNT(*) FROM VEICULOS WHERE PLACA LIKE 'TST%'
UNION ALL
SELECT 'multas orfas',               COUNT(*) FROM MULTAS M
 WHERE NOT EXISTS (SELECT 1 FROM VEICULOS V WHERE V.ID = M.VEICULO_ID)
UNION ALL
SELECT 'contratacoes orfas',         COUNT(*) FROM CONTRATACOES C
 WHERE NOT EXISTS (SELECT 1 FROM CLIENTES CL WHERE CL.ID = C.CLIENTE_ID);

SELECT LOGIN, NOME, ATIVO, ADMINISTRADOR FROM USUARIOS ORDER BY ID;

-- A tabela de precos (TIPOS_SERVICO), os roteiros das etapas e os dados
-- do escritorio NAO saem: sao catalogo, nao teste.
SELECT COUNT(*) AS servicos_na_tabela_de_precos FROM TIPOS_SERVICO;
