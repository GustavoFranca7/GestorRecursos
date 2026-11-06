-- =====================================================================
-- GESTOR DE RECURSOS - usuario de banco para as estacoes de trabalho
-- =====================================================================
-- NAO RODE ESTE ARQUIVO DE UMA VEZ SO.
--
-- Sao duas partes, com conexoes diferentes:
--
--     PARTE 1  conectado como ADMIN
--     PARTE 2  conectado como GESTOR
--
-- Rodar tudo como GESTOR falha: criar usuario e privilegio do ADMIN, e
-- sem o usuario criado todos os GRANT da parte 2 caem junto.
--
-- =====================================================================
-- POR QUE ESTE USUARIO EXISTE
--
-- O instalador leva uma senha de banco dentro dele, gravada no
-- appsettings.json ao lado do executavel. Quem tiver o instalador, ou a
-- maquina onde ele foi instalado, le essa senha.
--
-- Se a senha for a do GESTOR, isso entrega o dono do esquema: quem pode
-- criar, alterar e APAGAR tabela, e com ela todo o historico do
-- escritorio. O ESTACAO le e grava os dados, e so isso: nenhum DROP,
-- nenhum ALTER, nenhum TRUNCATE.
--
-- O GESTOR continua dono das tabelas e usuario de desenvolvimento.
-- Nada do que ja existe muda.
--
-- =====================================================================
-- ATENCAO: TABELA NOVA NASCE INVISIVEL PARA A ESTACAO
--
-- A parte 2 percorre as tabelas que existem NO DIA em que ela roda. Toda
-- tabela criada depois comeca sem privilegio nenhum, e o programa a
-- enxerga como inexistente - ORA-00942 - mesmo com a tabela criada e o
-- script de criacao acusando sucesso.
--
-- Script que CRIA tabela precisa terminar com o GRANT dela. O modelo
-- esta no passo 4 do 12-RegistroDoBackup.sql: libera os quatro direitos
-- na tabela, mais SELECT na sequence da coluna IDENTITY, e nao faz nada
-- quando o usuario ESTACAO nao existe (o caso do banco de teste).
--
-- Script que so ALTERA tabela existente nao precisa: o privilegio ja
-- esta dado na tabela, e acompanha a coluna nova.
-- =====================================================================


-- =====================================================================
-- PARTE 1  -  conectado como ADMIN
-- =====================================================================
-- Troque a senha antes de executar. Regras do Autonomous: 12 a 30
-- caracteres, com maiuscula, minuscula e numero, sem aspas duplas e sem
-- o nome do usuario dentro.

CREATE USER ESTACAO IDENTIFIED BY "TroqueEstaSenha123";

-- Sem QUOTA e sem RESOURCE: este usuario nao cria objeto nenhum.
GRANT CREATE SESSION TO ESTACAO;

-- Confira que existe antes de passar para a parte 2:
SELECT username, account_status FROM dba_users WHERE username = 'ESTACAO';


-- =====================================================================
-- PARTE 2  -  conectado como GESTOR
-- =====================================================================
-- Agora saia do ADMIN e entre como GESTOR, que e o dono das tabelas.
--
-- Nao ha sinonimo aqui de proposito. O programa diz ao banco, ao
-- conectar, em que esquema procurar (ALTER SESSION SET CURRENT_SCHEMA),
-- o que evita dezenas de sinonimos e o privilegio amplo que criar cada
-- um deles exigiria.

DECLARE
    existe NUMBER;
BEGIN
    -- Avisa de uma vez, em vez de falhar tabela por tabela, se a parte 1
    -- nao tiver sido feita.
    SELECT COUNT(*) INTO existe FROM all_users WHERE username = 'ESTACAO';
    IF existe = 0 THEN
        RAISE_APPLICATION_ERROR(-20001,
            'O usuario ESTACAO nao existe. Rode a PARTE 1 conectado como ADMIN.');
    END IF;

    FOR t IN (SELECT table_name FROM user_tables ORDER BY table_name) LOOP
        EXECUTE IMMEDIATE
            'GRANT SELECT, INSERT, UPDATE, DELETE ON ' || t.table_name || ' TO ESTACAO';
    END LOOP;

    -- As visoes financeiras sao so de leitura: elas calculam, nao guardam.
    FOR v IN (SELECT view_name FROM user_views ORDER BY view_name) LOOP
        EXECUTE IMMEDIATE 'GRANT SELECT ON ' || v.view_name || ' TO ESTACAO';
    END LOOP;

    -- As tabelas usam IDENTITY, que cria sequence propria. Sem isto o
    -- INSERT falha ao pedir o proximo numero.
    FOR s IN (SELECT sequence_name FROM user_sequences ORDER BY sequence_name) LOOP
        EXECUTE IMMEDIATE 'GRANT SELECT ON ' || s.sequence_name || ' TO ESTACAO';
    END LOOP;
END;
/

COMMIT;


-- =====================================================================
-- Conferencia  -  ainda como GESTOR
-- =====================================================================
-- Deve dar 21 tabelas + 2 visoes, e as sequences das colunas IDENTITY.

SELECT COUNT(DISTINCT table_name) AS objetos_liberados
  FROM user_tab_privs_made
 WHERE grantee = 'ESTACAO';

SELECT table_name, LISTAGG(privilege, ', ') WITHIN GROUP (ORDER BY privilege) AS direitos
  FROM user_tab_privs_made
 WHERE grantee = 'ESTACAO'
 GROUP BY table_name
 ORDER BY table_name;


-- =====================================================================
-- Depois de rodar
-- =====================================================================
-- No appsettings.json que vai no instalador, trocar o usuario e a senha
-- e acrescentar o esquema:
--
--     "ConnectionStrings": {
--       "Oracle": "User Id=ESTACAO;Password=A_SENHA_NOVA;Data Source=...;"
--     },
--     "Oracle": { "TnsAdmin": "wallet", "Esquema": "GESTOR" }
--
-- Sem a chave Esquema o programa conecta mas nao acha tabela nenhuma:
-- o ESTACAO nao tem tabela propria.
--
-- Para tirar o acesso de uma estacao sem mexer em mais nada, como ADMIN:
--
--     ALTER USER ESTACAO ACCOUNT LOCK;
--
-- Para desfazer este script por inteiro, como ADMIN:
--
--     DROP USER ESTACAO CASCADE;
