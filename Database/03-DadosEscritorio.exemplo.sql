-- =====================================================================
-- GESTOR DE RECURSOS - dados do escritorio (MODELO)
-- =====================================================================
-- Este arquivo e um MODELO e vai para o GitHub com dados falsos.
--
-- Para usar:
--   1. Copie para "03-DadosEscritorio.sql" (sem o ".exemplo")
--   2. Troque os valores pelos reais
--   3. Rode no banco
--
-- O arquivo sem ".exemplo" esta no .gitignore: ele carrega CPF e RG de
-- uma pessoa real, e isso nao entra em repositorio publico.
-- Os valores reais estao no cofre, em "4 Referencias\Dados do escritorio".
-- =====================================================================

MERGE INTO ESCRITORIO d
USING (SELECT 1 AS ID FROM DUAL) s
   ON (d.ID = s.ID)
WHEN MATCHED THEN UPDATE SET
    NOME                  = 'NOME COMPLETO AQUI',
    NACIONALIDADE         = 'Brasileira',
    ESTADO_CIVIL          = 'Solteira',
    PROFISSAO             = 'Bacharel em Direito e Despachante Documentalista',
    RG                    = '0000000',
    RG_ORGAO              = 'SSP SC',
    CPF                   = '00000000000',
    REGISTRO_PROFISSIONAL = NULL,
    EMAIL                 = 'email@exemplo.com',
    LOGRADOURO            = 'Rua Exemplo',
    NUMERO                = '000',
    COMPLEMENTO           = NULL,
    BAIRRO                = 'Bairro',
    CEP                   = '00000000',
    CIDADE                = 'Balneario Camboriu',
    UF                    = 'SC',
    CIDADE_ASSINATURA     = 'Balneario Camboriu'
WHEN NOT MATCHED THEN INSERT (
    ID, NOME, NACIONALIDADE, ESTADO_CIVIL, PROFISSAO, RG, RG_ORGAO, CPF,
    REGISTRO_PROFISSIONAL, EMAIL, LOGRADOURO, NUMERO, COMPLEMENTO, BAIRRO,
    CEP, CIDADE, UF, CIDADE_ASSINATURA
) VALUES (
    1, 'NOME COMPLETO AQUI', 'Brasileira', 'Solteira',
    'Bacharel em Direito e Despachante Documentalista',
    '0000000', 'SSP SC', '00000000000',
    NULL, 'email@exemplo.com', 'Rua Exemplo', '000', NULL, 'Bairro',
    '00000000', 'Balneario Camboriu', 'SC', 'Balneario Camboriu'
);

COMMIT;

-- MERGE em vez de INSERT: rodar duas vezes nao quebra nem duplica.
SELECT NOME, CIDADE, UF FROM ESCRITORIO;
