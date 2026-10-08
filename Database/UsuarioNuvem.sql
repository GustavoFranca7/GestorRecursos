-- ============================================================
-- Usuario da aplicacao no Oracle Autonomous Database (nuvem)
-- Rodar UMA vez, conectado como ADMIN, antes do CriacaoBanco.sql.
--
-- Troque a senha antes de executar. Regras do Autonomous:
-- 12 a 30 caracteres, com letra maiuscula, minuscula, numero,
-- sem aspas duplas e sem a palavra do nome do usuario.
-- ============================================================

CREATE USER GESTOR IDENTIFIED BY "TroqueEstaSenha123"
    QUOTA UNLIMITED ON DATA;

-- CONNECT: pode abrir sessao. RESOURCE: pode criar tabelas, sequences e views.
GRANT CONNECT, RESOURCE TO GESTOR;

-- Libera o usuario nas ferramentas web do console (Database Actions).
BEGIN
    ORDS_ADMIN.ENABLE_SCHEMA(
        p_enabled             => TRUE,
        p_schema              => 'GESTOR',
        p_url_mapping_type    => 'BASE_PATH',
        p_url_mapping_pattern => 'gestor',
        p_auto_rest_auth      => TRUE
    );
    COMMIT;
END;
/
