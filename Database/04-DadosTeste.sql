-- =====================================================================
-- GESTOR DE RECURSOS - dados de teste
-- =====================================================================
-- Povoa o banco com 30 clientes ficticios, completos o bastante para
-- conferir todas as telas: ficha com dados pessoais, CNH e endereco,
-- veiculos e multas, processos nas tres instancias, financeiro com
-- pagamentos, despesas e creditos, e compromissos na agenda.
--
-- CONVENCAO (combinada): tudo que e teste tem NOME comecando em "TESTE "
-- e CPF comecando em 900000000. Placas, AIT e numeros de processo
-- comecam em TST. Nada fora desse padrao e tocado por este script.
--
-- Pode rodar quantas vezes quiser: a primeira parte apaga os registros
-- de teste anteriores antes de criar os novos. Todas as datas sao
-- relativas a hoje, entao os prazos continuam fazendo sentido com o
-- tempo.
--
-- COMO RODAR
-- ------------------------------------------------------------------
-- No SQL Developer Web, use **Executar script** (F5, o botao com a
-- folha), e nao "Executar instrucao" (Ctrl+Enter): a parte 2 e um
-- bloco unico, terminado por "/" em linha propria.
--
-- Nada e confirmado antes do fim. Se a carga falhar, o ROLLBACK desfaz
-- tambem a limpeza do inicio e o banco volta como estava. A mensagem
-- diz em qual cliente parou:
--     ORA-20001: Falhou em: cliente 7 - TESTE ... >>> ORA-xxxxx ...
-- =====================================================================

SET SERVEROUTPUT ON;


-- ---------------------------------------------------------------------
-- 1. Limpa o teste anterior
-- ---------------------------------------------------------------------
-- A ordem e de dentro para fora: o que aponta sai antes do que e apontado.
DELETE FROM AGENDA
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

DELETE FROM MULTAS         WHERE AIT LIKE 'TST%';
DELETE FROM CLIENTE_VEICULO
 WHERE VEICULO_ID IN (SELECT ID FROM VEICULOS WHERE PLACA LIKE 'TST%');
DELETE FROM VEICULOS       WHERE PLACA LIKE 'TST%';
DELETE FROM ENDERECOS
 WHERE CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%');
DELETE FROM CLIENTES       WHERE CPF LIKE '900000000%';

-- De proposito, sem COMMIT aqui. A limpeza so vale junto com a carga: se a parte 2
-- falhar, o ROLLBACK dela desfaz tambem estes DELETE e o banco volta como estava.
-- Era o que faltava quando o script apagou tudo e nao inseriu nada (06/10).


-- ---------------------------------------------------------------------
-- 2. Cria os 30 clientes
-- ---------------------------------------------------------------------
DECLARE

    -- O Oracle exige as variaveis antes dos subprogramas na mesma secao.
    --
    -- v_etapa diz em que ponto o script estava quando algo deu errado. Sem ela, um
    -- erro no meio de 30 clientes vira uma mensagem sem endereco.
    v_etapa VARCHAR2(200) := 'inicio';
    v_cli  NUMBER;
    v_vei  NUMBER;
    v_vei2 NUMBER;
    v_mul  NUMBER;
    v_mul2 NUMBER;
    v_mul3 NUMBER;
    v_pro  NUMBER;
    v_con  NUMBER;
    v_ite  NUMBER;

    -- Cada bloco abaixo monta uma peca. Quem chama decide o que cada
    -- cliente tem: ha quem so tenha cadastro, e ha quem tenha dois
    -- veiculos, tres instancias e historico de pagamento.

    FUNCTION id_servico(p_nome IN VARCHAR2) RETURN NUMBER IS
        v_id NUMBER;
    BEGIN
        SELECT ID INTO v_id FROM TIPOS_SERVICO WHERE NOME = p_nome;
        RETURN v_id;
    EXCEPTION
        -- Nome fora da tabela de precos daria um "no data found" seco, sem dizer qual.
        WHEN NO_DATA_FOUND THEN
            RAISE_APPLICATION_ERROR(-20002,
                'Servico nao encontrado em TIPOS_SERVICO: "' || p_nome
                || '". Confira se 02-CargaInicial.sql foi rodado.');
    END id_servico;


    FUNCTION novo_cliente(
        p_seq           IN NUMBER,
        p_nome          IN VARCHAR2,
        p_profissao     IN VARCHAR2 DEFAULT NULL,
        p_estado_civil  IN VARCHAR2 DEFAULT NULL,
        p_com_cnh       IN CHAR     DEFAULT 'S',
        p_observacoes   IN VARCHAR2 DEFAULT NULL,
        p_arquivado     IN CHAR     DEFAULT 'N') RETURN NUMBER
    IS
        v_id NUMBER;
    BEGIN
        INSERT INTO CLIENTES (
            NOME, CPF, EMAIL, WHATSAPP,
            CNH_NUMERO, CNH_CATEGORIA, CNH_VALIDADE, CNH_REGISTRO,
            RG, RG_ORGAO, NACIONALIDADE, ESTADO_CIVIL, PROFISSAO,
            OBSERVACOES, ARQUIVADO, DATA_CADASTRO)
        VALUES (
            p_nome,
            '900000000' || LPAD(p_seq, 2, '0'),
            'teste' || p_seq || '@exemplo.com',
            '479' || LPAD(p_seq, 2, '0') || '000000',
            CASE WHEN p_com_cnh = 'S' THEN LPAD(p_seq, 11, '7') END,
            CASE WHEN p_com_cnh = 'S' THEN
                CASE MOD(p_seq, 4) WHEN 0 THEN 'AB' WHEN 1 THEN 'B'
                                   WHEN 2 THEN 'AD' ELSE 'A' END
            END,
            CASE WHEN p_com_cnh = 'S' THEN TRUNC(SYSDATE) + 120 + (p_seq * 23) END,
            CASE WHEN p_com_cnh = 'S' THEN LPAD(p_seq, 10, '5') END,
            '4' || LPAD(p_seq, 6, '3'),
            'SSP/SC',
            'Brasileira',
            p_estado_civil,
            p_profissao,
            p_observacoes,
            p_arquivado,
            TRUNC(SYSDATE) - (p_seq * 11))
        RETURNING ID INTO v_id;

        RETURN v_id;
    END novo_cliente;


    PROCEDURE novo_endereco(
        p_cliente IN NUMBER,
        p_rua     IN VARCHAR2,
        p_numero  IN VARCHAR2,
        p_bairro  IN VARCHAR2,
        p_cidade  IN VARCHAR2,
        p_uf      IN VARCHAR2,
        p_cep     IN VARCHAR2,
        p_compl   IN VARCHAR2 DEFAULT NULL)
    IS
    BEGIN
        INSERT INTO ENDERECOS (CLIENTE_ID, CEP, LOGRADOURO, NUMERO, COMPLEMENTO, BAIRRO, CIDADE, ESTADO)
        VALUES (p_cliente, p_cep, p_rua, p_numero, p_compl, p_bairro, p_cidade, p_uf);
    END novo_endereco;


    FUNCTION novo_veiculo(
        p_cliente IN NUMBER,
        p_sufixo  IN VARCHAR2,
        p_marca   IN VARCHAR2,
        p_cor     IN VARCHAR2,
        p_ano     IN VARCHAR2,
        p_vinculo IN VARCHAR2 DEFAULT 'PROPRIETARIO') RETURN NUMBER
    IS
        v_id NUMBER;
    BEGIN
        INSERT INTO VEICULOS (PLACA, RENAVAM, MARCA_MODELO, COR, ANO)
        VALUES ('TST' || p_sufixo, LPAD(p_sufixo, 11, '9'), p_marca, p_cor, p_ano)
        RETURNING ID INTO v_id;

        INSERT INTO CLIENTE_VEICULO (CLIENTE_ID, VEICULO_ID, VINCULO)
        VALUES (p_cliente, v_id, p_vinculo);

        RETURN v_id;
    END novo_veiculo;


    FUNCTION nova_multa(
        p_veiculo     IN NUMBER,
        p_sufixo      IN VARCHAR2,
        p_gravidade   IN VARCHAR2,
        p_pontos      IN NUMBER,
        p_valor       IN NUMBER,
        p_artigo      IN VARCHAR2,
        p_descricao   IN VARCHAR2,
        p_dias_atras  IN NUMBER DEFAULT 45) RETURN NUMBER
    IS
        v_id NUMBER;
    BEGIN
        INSERT INTO MULTAS (
            VEICULO_ID, AIT, ORGAO_AUTUADOR, CODIGO_ORGAO, DATA_HORA_INFRACAO,
            LOCAL_INFRACAO, ENQUADRAMENTO, DESCRICAO, GRAVIDADE, PONTOS, VALOR,
            PRAZO_DEFESA, VENCIMENTO_BOLETO)
        VALUES (
            p_veiculo, 'TST' || p_sufixo, 'PREFEITURA MUNICIPAL DE TESTE', '280110',
            TRUNC(SYSDATE) - p_dias_atras,
            'Avenida Brasil, em frente ao numero 1000',
            p_artigo, p_descricao, p_gravidade, p_pontos, p_valor,
            TRUNC(SYSDATE) - p_dias_atras + 30,
            TRUNC(SYSDATE) - p_dias_atras + 45)
        RETURNING ID INTO v_id;

        RETURN v_id;
    END nova_multa;


    FUNCTION novo_processo_cnh(
        p_cliente     IN NUMBER,
        p_sufixo      IN VARCHAR2,
        p_tipo        IN VARCHAR2,
        p_motivo      IN VARCHAR2,
        p_dias_notif  IN NUMBER,
        p_prazo_dias  IN NUMBER) RETURN NUMBER
    IS
        v_id NUMBER;
    BEGIN
        INSERT INTO PROCESSOS_CNH (
            CLIENTE_ID, NUMERO_PROCESSO, TIPO, MOTIVO,
            DATA_NOTIFICACAO, PRAZO_DEFESA, PORTARIA, ORGAO_INSTAURADOR)
        VALUES (
            p_cliente, 'TST' || p_sufixo, p_tipo, p_motivo,
            TRUNC(SYSDATE) - p_dias_notif,
            TRUNC(SYSDATE) + p_prazo_dias,
            'Portaria TST-' || p_sufixo,
            'DETRAN/SC')
        RETURNING ID INTO v_id;

        RETURN v_id;
    END novo_processo_cnh;


    PROCEDURE liga_processo_multa(p_processo IN NUMBER, p_multa IN NUMBER) IS
    BEGIN
        INSERT INTO PROCESSO_MULTA (PROCESSO_CNH_ID, MULTA_ID, OBSERVACAO)
        VALUES (p_processo, p_multa, 'Multa que compoe a pontuacao questionada.');
    END liga_processo_multa;


    FUNCTION nova_contratacao(
        p_cliente     IN NUMBER,
        p_tabela      IN NUMBER,
        p_desconto    IN NUMBER DEFAULT 0,
        p_dias_atras  IN NUMBER DEFAULT 25,
        p_situacao    IN VARCHAR2 DEFAULT 'ANDAMENTO') RETURN NUMBER
    IS
        v_id NUMBER;
    BEGIN
        INSERT INTO CONTRATACOES (
            CLIENTE_ID, DATA_CONTRATACAO, VALOR_TABELA, DESCONTO, VALOR_FINAL, SITUACAO, OBSERVACOES)
        VALUES (
            p_cliente, TRUNC(SYSDATE) - p_dias_atras, p_tabela, p_desconto,
            p_tabela - p_desconto, p_situacao,
            CASE WHEN p_desconto > 0 THEN 'Desconto combinado na negociacao.' END)
        RETURNING ID INTO v_id;

        RETURN v_id;
    END nova_contratacao;


    FUNCTION novo_item(
        p_contratacao IN NUMBER,
        p_servico     IN VARCHAR2,
        p_multa       IN NUMBER DEFAULT NULL,
        p_processo    IN NUMBER DEFAULT NULL,
        p_situacao    IN VARCHAR2 DEFAULT 'ANDAMENTO') RETURN NUMBER
    IS
        v_id      NUMBER;
        v_servico NUMBER;
    BEGIN
        -- A busca vem antes do INSERT, e nao dentro dele: funcao declarada num bloco
        -- anonimo nao existe para o motor SQL (PLS-00231).
        v_servico := id_servico(p_servico);

        INSERT INTO ITENS_CONTRATACAO (CONTRATACAO_ID, TIPO_SERVICO_ID, MULTA_ID, PROCESSO_CNH_ID, SITUACAO)
        VALUES (p_contratacao, v_servico, p_multa, p_processo, p_situacao)
        RETURNING ID INTO v_id;

        RETURN v_id;
    END novo_item;


    -- Instancia com DATA_PROTOCOLO nula e a que faz o prazo contar nas telas.
    -- Com RESULTADO preenchido, ela ja terminou e vira historico.
    PROCEDURE nova_instancia(
        p_item            IN NUMBER,
        p_fase            IN VARCHAR2,
        p_situacao        IN VARCHAR2,
        p_prazo_dias      IN NUMBER   DEFAULT NULL,
        p_protocolo_dias  IN NUMBER   DEFAULT NULL,
        p_resultado       IN VARCHAR2 DEFAULT NULL,
        p_resultado_dias  IN NUMBER   DEFAULT NULL,
        p_orgao           IN VARCHAR2 DEFAULT 'PREFEITURA MUNICIPAL DE TESTE')
    IS
    BEGIN
        INSERT INTO INSTANCIAS (
            ITEM_ID, FASE, SITUACAO, PRAZO_PROTOCOLO, DATA_PROTOCOLO,
            NUMERO_PROTOCOLO, ORGAO_DESTINATARIO, DATA_RESULTADO, RESULTADO, OBSERVACOES)
        VALUES (
            p_item, p_fase, p_situacao,
            CASE WHEN p_prazo_dias IS NOT NULL THEN TRUNC(SYSDATE) + p_prazo_dias END,
            CASE WHEN p_protocolo_dias IS NOT NULL THEN TRUNC(SYSDATE) - p_protocolo_dias END,
            CASE WHEN p_protocolo_dias IS NOT NULL THEN 'TSTP' || p_item || SUBSTR(p_fase, 1, 1) END,
            p_orgao,
            CASE WHEN p_resultado_dias IS NOT NULL THEN TRUNC(SYSDATE) - p_resultado_dias END,
            p_resultado,
            CASE WHEN p_resultado = 'INDEFERIDO' THEN 'Seguiu para a proxima instancia.' END);
    END nova_instancia;


    PROCEDURE novo_pagamento(
        p_contratacao IN NUMBER,
        p_valor       IN NUMBER,
        p_forma       IN VARCHAR2 DEFAULT 'PIX',
        p_dias_atras  IN NUMBER   DEFAULT 15,
        p_parcelas    IN NUMBER   DEFAULT 1)
    IS
    BEGIN
        INSERT INTO PAGAMENTOS (CONTRATACAO_ID, DATA_PAGAMENTO, VALOR, FORMA, PARCELAS, OBSERVACAO)
        VALUES (p_contratacao, TRUNC(SYSDATE) - p_dias_atras, p_valor, p_forma, p_parcelas,
                CASE WHEN p_parcelas > 1 THEN 'Parcelado em ' || p_parcelas || 'x.' END);
    END novo_pagamento;


    PROCEDURE nova_despesa(
        p_contratacao IN NUMBER,
        p_tipo        IN VARCHAR2,
        p_valor       IN NUMBER,
        p_dias_atras  IN NUMBER DEFAULT 20)
    IS
    BEGIN
        INSERT INTO DESPESAS (CONTRATACAO_ID, TIPO, VALOR, DATA_DESPESA)
        VALUES (p_contratacao, p_tipo, p_valor, TRUNC(SYSDATE) - p_dias_atras);
    END nova_despesa;


    PROCEDURE novo_credito(
        p_cliente IN NUMBER,
        p_valor   IN NUMBER,
        p_origem  IN VARCHAR2)
    IS
    BEGIN
        INSERT INTO CREDITOS (CLIENTE_ID, VALOR, ORIGEM, UTILIZADO, DATA_CREDITO)
        VALUES (p_cliente, p_valor, p_origem, 'N', TRUNC(SYSDATE) - 10);
    END novo_credito;


    -- Serviço direto nao tem instancia: tem roteiro. Copia as etapas do modelo
    -- para o item e marca as primeiras como ja cumpridas.
    PROCEDURE copiar_etapas(
        p_item       IN NUMBER,
        p_servico    IN VARCHAR2,
        p_concluidas IN NUMBER DEFAULT 0)
    IS
    BEGIN
        INSERT INTO ETAPAS (ITEM_ID, ETAPA_MODELO_ID, CONCLUIDA, DATA_CONCLUSAO)
        SELECT p_item, EM.ID,
               CASE WHEN EM.ORDEM <= p_concluidas THEN 'S' ELSE 'N' END,
               CASE WHEN EM.ORDEM <= p_concluidas THEN TRUNC(SYSDATE) - (12 - EM.ORDEM) END
          FROM ETAPAS_MODELO EM
          JOIN TIPOS_SERVICO TS ON TS.ID = EM.TIPO_SERVICO_ID
         WHERE TS.NOME = p_servico;
    END copiar_etapas;


    PROCEDURE novo_compromisso(
        p_dias    IN NUMBER,
        p_tipo    IN VARCHAR2,
        p_titulo  IN VARCHAR2,
        p_cliente IN NUMBER,
        p_hora    IN VARCHAR2 DEFAULT NULL)
    IS
    BEGIN
        INSERT INTO AGENDA (DATA_AGENDA, HORA, TIPO, TITULO, CLIENTE_ID, CONCLUIDO)
        VALUES (TRUNC(SYSDATE) + p_dias, p_hora, p_tipo, p_titulo, p_cliente,
                CASE WHEN p_dias < 0 THEN 'S' ELSE 'N' END);
    END novo_compromisso;


BEGIN

    -- =============================================================
    -- 1. Caso completo: dois veiculos, tres multas, recurso que ja
    --    passou pela autuacao e esta na JARI, pagamento parcial.
    -- =============================================================
    v_etapa := 'cliente 1 - TESTE Ana Paula Krieger';
    v_cli := novo_cliente(1, 'TESTE Ana Paula Krieger', 'Coordenadora escolar', 'Casada',
                          p_observacoes => 'Prefere contato por WhatsApp depois das 18h.');
    novo_endereco(v_cli, 'Rua Belo Horizonte', '450', 'Areias', 'Camboriu', 'SC', '88345067', 'ap 104');
    v_vei  := novo_veiculo(v_cli, '0001', 'Nissan Versa', 'Preta', '2019/2019');
    v_vei2 := novo_veiculo(v_cli, '0002', 'Fiat Argo', 'Branca', '2021/2021', 'CONDUTOR');
    v_mul  := nova_multa(v_vei, '000001', 'GRAVISSIMA', 7, 880.41, '218 III',
                         'Transitar em velocidade superior a maxima permitida em mais de 50%', 60);
    v_mul2 := nova_multa(v_vei, '000002', 'MEDIA', 4, 130.16, '181 VIII',
                         'Estacionar em desacordo com a regulamentacao', 40);
    v_mul3 := nova_multa(v_vei2, '000003', 'GRAVE', 5, 195.23, '208',
                         'Avancar o sinal vermelho do semaforo', 30);

    v_con := nova_contratacao(v_cli, 300.00, 0, 30);
    v_ite := novo_item(v_con, 'Recurso de multa gravissima', v_mul);
    nova_instancia(v_ite, 'AUTUACAO', 'INDEFERIDO', NULL, 50, 'INDEFERIDO', 20);
    nova_instancia(v_ite, 'JARI', 'AGUARDANDO_JULGAMENTO', NULL, 12, p_orgao => 'JARI/SC');
    v_ite := novo_item(v_con, 'Recurso de multa media', v_mul2);
    nova_instancia(v_ite, 'AUTUACAO', 'AGUARDANDO_DOCUMENTO', 2);
    novo_pagamento(v_con, 150.00, 'PIX', 20);
    nova_despesa(v_con, 'Correio - AR', 18.50, 25);
    novo_compromisso(2, 'PRAZO', 'Protocolar defesa previa - AIT TST000002', v_cli, '09:00');

    -- =============================================================
    -- 2. Suspensao do direito de dirigir, com as multas da pontuacao
    -- =============================================================
    v_etapa := 'cliente 2 - TESTE Bruno Vargas Arruda';
    v_cli := novo_cliente(2, 'TESTE Bruno Vargas Arruda', 'Motorista de aplicativo', 'Solteiro',
                          p_observacoes => 'Depende da CNH para trabalhar. Caso urgente.');
    novo_endereco(v_cli, 'Rua das Palmeiras', '78', 'Centro', 'Balneario Camboriu', 'SC', '88330100');
    v_vei  := novo_veiculo(v_cli, '0003', 'Chevrolet Onix', 'Prata', '2020/2021');
    v_mul  := nova_multa(v_vei, '000004', 'GRAVISSIMA', 7, 293.47, '165',
                         'Dirigir sob influencia de alcool', 120);
    v_mul2 := nova_multa(v_vei, '000005', 'GRAVE', 5, 195.23, '244 I',
                         'Conduzir motocicleta sem capacete', 95);

    v_pro := novo_processo_cnh(v_cli, '50227', 'SUSPENSAO',
                               'Soma de 23 pontos em 12 meses', 15, 8);
    liga_processo_multa(v_pro, v_mul);
    liga_processo_multa(v_pro, v_mul2);

    v_con := nova_contratacao(v_cli, 1200.00, 200.00, 18);
    v_ite := novo_item(v_con, 'Suspensao do direito de dirigir sem indicacao', p_processo => v_pro);
    nova_instancia(v_ite, 'AUTUACAO', 'AGUARDANDO_DOCUMENTO', 8, p_orgao => 'DETRAN/SC');
    novo_pagamento(v_con, 500.00, 'PIX', 15);
    novo_compromisso(8, 'PRAZO', 'Protocolar defesa da suspensao - processo TST50227', v_cli, '14:00');

    -- =============================================================
    -- 3. Tudo pago e concluido: serve para ver a ficha sem alarme
    -- =============================================================
    v_etapa := 'cliente 3 - TESTE Carla Beatriz Nunes';
    v_cli := novo_cliente(3, 'TESTE Carla Beatriz Nunes', 'Professora', 'Divorciada');
    novo_endereco(v_cli, 'Rua Sao Pedro', '1200', 'Gravata', 'Navegantes', 'SC', '88375000');
    v_vei := novo_veiculo(v_cli, '0004', 'Hyundai HB20', 'Vermelho', '2018/2019');
    v_mul := nova_multa(v_vei, '000006', 'LEVE', 3, 88.38, '181 II',
                        'Estacionar afastado da guia mais de um metro', 180);

    v_con := nova_contratacao(v_cli, 40.00, 0, 150, 'CONCLUIDO');
    v_ite := novo_item(v_con, 'Recurso de multa leve', v_mul, p_situacao => 'CONCLUIDO');
    nova_instancia(v_ite, 'AUTUACAO', 'DEFERIDO', NULL, 140, 'DEFERIDO', 100);
    novo_pagamento(v_con, 40.00, 'DINHEIRO', 148);

    -- =============================================================
    -- 4. Prazo vencendo hoje, nada pago: o caso que grita na lista
    -- =============================================================
    v_etapa := 'cliente 4 - TESTE Diego Ramos Teixeira';
    v_cli := novo_cliente(4, 'TESTE Diego Ramos Teixeira', 'Autonomo', 'Solteiro');
    novo_endereco(v_cli, 'Rua Blumenau', '305', 'Sao Vicente', 'Itajai', 'SC', '88309200');
    v_vei := novo_veiculo(v_cli, '0005', 'Volkswagen Gol', 'Branco', '2015/2016');
    v_mul := nova_multa(v_vei, '000007', 'GRAVISSIMA', 7, 880.41, '218 III',
                        'Transitar em velocidade superior a maxima permitida em mais de 50%', 28);

    v_con := nova_contratacao(v_cli, 150.00, 0, 10);
    v_ite := novo_item(v_con, 'Recurso de multa gravissima', v_mul);
    nova_instancia(v_ite, 'AUTUACAO', 'AGUARDANDO_DOCUMENTO', 0);
    novo_compromisso(0, 'PRAZO', 'Prazo final - AIT TST000007', v_cli, '17:00');

    -- =============================================================
    -- 5. Cassacao, valor alto em aberto, dois pagamentos parciais
    -- =============================================================
    v_etapa := 'cliente 5 - TESTE Eduardo Stein';
    v_cli := novo_cliente(5, 'TESTE Eduardo Stein', 'Representante comercial', 'Casado');
    novo_endereco(v_cli, 'Avenida Ipiranga', '4455', 'Partenon', 'Porto Alegre', 'RS', '90610000', 'sala 2');
    v_vei := novo_veiculo(v_cli, '0006', 'Toyota Corolla', 'Cinza', '2022/2023');
    v_mul := nova_multa(v_vei, '000008', 'GRAVISSIMA', 7, 2934.70, '162 I',
                        'Dirigir sem possuir CNH', 200);

    v_pro := novo_processo_cnh(v_cli, '50310', 'CASSACAO',
                               'Reincidencia em infracao de natureza gravissima', 25, 12);
    liga_processo_multa(v_pro, v_mul);

    v_con := nova_contratacao(v_cli, 1200.00, 0, 22);
    v_ite := novo_item(v_con, 'Cassacao do direito de dirigir sem indicacao', p_processo => v_pro);
    nova_instancia(v_ite, 'AUTUACAO', 'PROTOCOLADO', NULL, 5, p_orgao => 'DETRAN/SC');
    novo_pagamento(v_con, 300.00, 'CREDITO', 20, 3);
    novo_pagamento(v_con, 200.00, 'PIX', 8);
    nova_despesa(v_con, 'Taxa DETRAN', 94.72, 18);

    -- =============================================================
    -- 6. Servico direto com checklist: reciclagem
    -- =============================================================
    v_etapa := 'cliente 6 - TESTE Fernanda Luz Prado';
    v_cli := novo_cliente(6, 'TESTE Fernanda Luz Prado', 'Enfermeira', 'Casada');
    novo_endereco(v_cli, 'Rua Sete de Setembro', '92', 'Velha', 'Blumenau', 'SC', '89046000');

    v_con := nova_contratacao(v_cli, 500.00, 50.00, 12);
    v_ite := novo_item(v_con, 'Reciclagem basica');
    copiar_etapas(v_ite, 'Reciclagem basica', 2);
    novo_pagamento(v_con, 450.00, 'DEBITO', 12);
    novo_compromisso(5, 'PROVA', 'Prova da reciclagem no DETRAN', v_cli, '08:30');

    -- =============================================================
    -- 7. Tres instancias: autuacao e JARI indeferidas, no CETRAN
    -- =============================================================
    v_etapa := 'cliente 7 - TESTE Giani Cristina Moraes';
    v_cli := novo_cliente(7, 'TESTE Giani Cristina Moraes', 'Contadora', 'Casada',
                          p_observacoes => 'Processo longo, ja passou por duas instancias.');
    novo_endereco(v_cli, 'Rua Antonio Carlos', '66', 'Meia Praia', 'Itapema', 'SC', '88220000');
    v_vei := novo_veiculo(v_cli, '0007', 'Jeep Renegade', 'Verde', '2021/2022');
    v_mul := nova_multa(v_vei, '000009', 'GRAVE', 5, 195.23, '230 V',
                        'Conduzir veiculo com registro de placa adulterada', 300);

    v_con := nova_contratacao(v_cli, 400.00, 0, 280);
    v_ite := novo_item(v_con, 'Recurso de multa grave', v_mul);
    nova_instancia(v_ite, 'AUTUACAO', 'INDEFERIDO', NULL, 270, 'INDEFERIDO', 220);
    nova_instancia(v_ite, 'JARI', 'INDEFERIDO', NULL, 200, 'INDEFERIDO', 90, 'JARI/SC');
    nova_instancia(v_ite, 'CETRAN', 'AGUARDANDO_JULGAMENTO', NULL, 60, p_orgao => 'CETRAN/SC');
    novo_pagamento(v_con, 200.00, 'PIX', 270);
    novo_pagamento(v_con, 100.00, 'PIX', 120);

    -- =============================================================
    -- 8. Cadastro enxuto: so o basico, sem CNH e sem veiculo
    -- =============================================================
    v_etapa := 'cliente 8 - TESTE Helio Marcondes';
    v_cli := novo_cliente(8, 'TESTE Helio Marcondes', p_com_cnh => 'N');
    novo_endereco(v_cli, 'Rua Lauro Linhares', '2010', 'Trindade', 'Florianopolis', 'SC', '88036003');

    -- =============================================================
    -- 9. Credito a usar e prazo amanha
    -- =============================================================
    v_etapa := 'cliente 9 - TESTE Iuri Fernandes da Silva';
    v_cli := novo_cliente(9, 'TESTE Iuri Fernandes da Silva', 'Pedreiro', 'Uniao estavel');
    novo_endereco(v_cli, 'Rua Primavera', '15', 'Itacolomi', 'Balneario Picarras', 'SC', '88380000');
    v_vei := novo_veiculo(v_cli, '0008', 'Honda Biz', 'Vermelha', '2017/2017');
    v_mul := nova_multa(v_vei, '000010', 'MEDIA', 4, 130.16, '244 VIII',
                        'Conduzir motocicleta transportando crianca menor de dez anos', 35);

    v_con := nova_contratacao(v_cli, 250.00, 0, 14);
    v_ite := novo_item(v_con, 'Recurso de multa media', v_mul);
    nova_instancia(v_ite, 'AUTUACAO', 'AGUARDANDO_DOCUMENTO', 1);
    novo_pagamento(v_con, 100.00, 'PIX', 10);
    novo_credito(v_cli, 60.00, 'Devolucao de servico cancelado em outra contratacao');

    -- =============================================================
    -- 10. Duas contratacoes: uma concluida, uma em andamento
    -- =============================================================
    v_etapa := 'cliente 10 - TESTE Juliana Reis Bastos';
    v_cli := novo_cliente(10, 'TESTE Juliana Reis Bastos', 'Advogada', 'Solteira');
    novo_endereco(v_cli, 'Rua Nove de Marco', '600', 'Centro', 'Joinville', 'SC', '89201400');
    v_vei := novo_veiculo(v_cli, '0009', 'Renault Kwid', 'Azul', '2023/2023');
    v_mul := nova_multa(v_vei, '000011', 'LEVE', 3, 88.38, '182 IV',
                        'Estacionar na contramao de direcao', 220);

    v_con := nova_contratacao(v_cli, 40.00, 0, 210, 'CONCLUIDO');
    v_ite := novo_item(v_con, 'Recurso de multa leve', v_mul, p_situacao => 'CONCLUIDO');
    nova_instancia(v_ite, 'AUTUACAO', 'DEFERIDO', NULL, 200, 'DEFERIDO', 170);
    novo_pagamento(v_con, 40.00, 'PIX', 208);

    v_con := nova_contratacao(v_cli, 270.00, 0, 5);
    v_ite := novo_item(v_con, 'Renovacao de CNH');
    copiar_etapas(v_ite, 'Renovacao de CNH', 1);
    novo_compromisso(3, 'DETRAN', 'Exame medico da renovacao', v_cli, '10:15');

    -- =============================================================
    -- 11 a 26. Casos variados, gerados em laco
    -- =============================================================
    DECLARE
        TYPE lista_texto IS TABLE OF VARCHAR2(80);

        v_nomes lista_texto := lista_texto(
            'TESTE Marcos Antonio Pereira',  'TESTE Natalia Souza Lemos',
            'TESTE Otavio Bittencourt',      'TESTE Priscila Andrade',
            'TESTE Rafael Omura',            'TESTE Simone Vieira Rocha',
            'TESTE Tiago Rautenberg',        'TESTE Ursula Heinzen',
            'TESTE Valdir Cardoso',          'TESTE Wagner Deschamps',
            'TESTE Yasmin Correa',           'TESTE Zilda Marques',
            'TESTE Alexandre Fontes',        'TESTE Beatriz Hoffmann',
            'TESTE Caio Mendonca',           'TESTE Daniela Kruger');

        v_cidades lista_texto := lista_texto(
            'Itajai', 'Tijucas', 'Brusque', 'Gaspar', 'Joinville', 'Camboriu',
            'Blumenau', 'Penha', 'Navegantes', 'Itapema', 'Barra Velha', 'Garuva',
            'Sao Jose', 'Palhoca', 'Biguacu', 'Tubarao');

        v_profissoes lista_texto := lista_texto(
            'Comerciante', 'Analista de sistemas', 'Aposentado', 'Vendedora',
            'Caminhoneiro', 'Cozinheira', 'Eletricista', 'Fisioterapeuta',
            'Garcom', 'Hoteleira', 'Instrutor de autoescola', 'Jornalista',
            'Mecanico', 'Nutricionista', 'Operador de maquinas', 'Psicologa');

        v_estados lista_texto := lista_texto(
            'Solteiro', 'Casado', 'Divorciado', 'Uniao estavel');

        v_gravidades lista_texto := lista_texto('LEVE', 'MEDIA', 'GRAVE', 'GRAVISSIMA');
        v_servicos   lista_texto := lista_texto(
            'Recurso de multa leve', 'Recurso de multa media',
            'Recurso de multa grave', 'Recurso de multa gravissima');

        v_seq      NUMBER;
        v_indice   NUMBER;
        v_grau     NUMBER;
        v_prazo    NUMBER;
        v_valor    NUMBER;
        v_pago     NUMBER;
    BEGIN
        FOR i IN 1 .. 16 LOOP
            v_seq    := 10 + i;
            v_indice := i;
            v_grau   := MOD(i, 4) + 1;

            -- Prazos espalhados: vencido, hoje, esta semana, mes que vem e sem prazo.
            v_prazo := CASE MOD(i, 5)
                           WHEN 0 THEN NULL
                           WHEN 1 THEN -4
                           WHEN 2 THEN 3
                           WHEN 3 THEN 11
                           ELSE 27
                       END;

            v_valor := CASE v_grau WHEN 1 THEN 40 WHEN 2 THEN 65 WHEN 3 THEN 100 ELSE 150 END;
            v_pago  := CASE MOD(i, 3) WHEN 0 THEN v_valor WHEN 1 THEN v_valor / 2 ELSE 0 END;

            v_etapa := 'cliente do laco ' || v_seq;
            v_cli := novo_cliente(
                v_seq,
                v_nomes(v_indice),
                v_profissoes(v_indice),
                v_estados(MOD(i, 4) + 1),
                p_com_cnh => CASE WHEN MOD(i, 7) = 0 THEN 'N' ELSE 'S' END);

            novo_endereco(v_cli, 'Rua Projetada ' || v_seq, TO_CHAR(100 + v_seq),
                          'Centro', v_cidades(v_indice), 'SC',
                          '883' || LPAD(v_seq, 2, '0') || '000');

            -- Um em cada cinco fica so com o cadastro, sem veiculo nem servico.
            IF MOD(i, 5) <> 0 THEN
                v_vei := novo_veiculo(v_cli, LPAD(100 + v_seq, 4, '0'),
                                      'TESTE Modelo ' || v_grau, 'Prata', '2019/2020');

                v_mul := nova_multa(v_vei, LPAD(100 + v_seq, 6, '0'),
                                    v_gravidades(v_grau),
                                    CASE v_grau WHEN 1 THEN 3 WHEN 2 THEN 4 WHEN 3 THEN 5 ELSE 7 END,
                                    CASE v_grau WHEN 1 THEN 88.38 WHEN 2 THEN 130.16
                                                WHEN 3 THEN 195.23 ELSE 293.47 END,
                                    '218 I', 'Infracao de teste de grau ' || v_grau,
                                    30 + i);

                v_con := nova_contratacao(v_cli, v_valor, 0, 20 + i);
                v_ite := novo_item(v_con, v_servicos(v_grau), v_mul);

                IF v_prazo IS NULL THEN
                    nova_instancia(v_ite, 'AUTUACAO', 'PROTOCOLADO', NULL, 7);
                ELSE
                    nova_instancia(v_ite,
                                   CASE MOD(i, 3) WHEN 0 THEN 'AUTUACAO'
                                                  WHEN 1 THEN 'JARI' ELSE 'CETRAN' END,
                                   'AGUARDANDO_DOCUMENTO', v_prazo);
                END IF;

                IF v_pago > 0 THEN
                    novo_pagamento(v_con, v_pago,
                                   CASE MOD(i, 4) WHEN 0 THEN 'DINHEIRO' WHEN 1 THEN 'PIX'
                                                  WHEN 2 THEN 'DEBITO' ELSE 'CREDITO' END,
                                   10 + i);
                END IF;
            END IF;
        END LOOP;
    END;

    -- =============================================================
    -- 27 e 28. Arquivados, para a area de Arquivados ter o que mostrar
    -- =============================================================
    v_etapa := 'cliente 27 - TESTE Edson Thives';
    v_cli := novo_cliente(27, 'TESTE Edson Thives', 'Aposentado', 'Viuvo', p_arquivado => 'S');
    novo_endereco(v_cli, 'Rua do Sol', '30', 'Centro', 'Itapema', 'SC', '88220100');

    v_etapa := 'cliente 28 - TESTE Vanessa Klein';
    v_cli := novo_cliente(28, 'TESTE Vanessa Klein', 'Designer', 'Solteira', p_arquivado => 'S');
    novo_endereco(v_cli, 'Rua das Flores', '77', 'Centro', 'Navegantes', 'SC', '88375100');
    v_vei := novo_veiculo(v_cli, '0028', 'Fiat Mobi', 'Branco', '2020/2020');
    v_con := nova_contratacao(v_cli, 65.00, 0, 200, 'CONCLUIDO');
    v_ite := novo_item(v_con, 'Recurso de multa media', p_situacao => 'CONCLUIDO');
    novo_pagamento(v_con, 65.00, 'PIX', 198);

    -- =============================================================
    -- 29. Contratacao cancelada: nao pode entrar nas contas
    -- =============================================================
    v_etapa := 'cliente 29 - TESTE Rodrigo Bastos';
    v_cli := novo_cliente(29, 'TESTE Rodrigo Bastos', 'Tecnico em edificacoes', 'Casado');
    novo_endereco(v_cli, 'Rua Curitiba', '900', 'Fazenda', 'Itajai', 'SC', '88301300');
    v_vei := novo_veiculo(v_cli, '0029', 'Ford Ka', 'Preto', '2016/2017');
    v_con := nova_contratacao(v_cli, 100.00, 0, 60, 'CANCELADO');
    v_ite := novo_item(v_con, 'Recurso de multa grave', p_situacao => 'CANCELADO');
    nova_instancia(v_ite, 'AUTUACAO', 'DESISTENCIA');
    novo_credito(v_cli, 100.00, 'Contratacao cancelada a pedido do cliente');

    -- =============================================================
    -- 30. Tres servicos na mesma contratacao, com desconto
    -- =============================================================
    v_etapa := 'cliente 30 - TESTE Luciana Petry';
    v_cli := novo_cliente(30, 'TESTE Luciana Petry', 'Empresaria', 'Casada',
                          p_observacoes => 'Fechou pacote com tres servicos de uma vez.');
    novo_endereco(v_cli, 'Avenida Central', '1500', 'Centro', 'Balneario Camboriu', 'SC', '88330200', 'sala 801');
    v_vei  := novo_veiculo(v_cli, '0030', 'BMW 320i', 'Branco', '2023/2024');
    v_mul  := nova_multa(v_vei, '000030', 'GRAVISSIMA', 7, 880.41, '218 III',
                         'Transitar em velocidade superior a maxima permitida em mais de 50%', 50);
    v_mul2 := nova_multa(v_vei, '000031', 'GRAVE', 5, 195.23, '208',
                         'Avancar o sinal vermelho do semaforo', 48);
    v_mul3 := nova_multa(v_vei, '000032', 'MEDIA', 4, 130.16, '181 VIII',
                         'Estacionar em desacordo com a regulamentacao', 46);

    v_con := nova_contratacao(v_cli, 315.00, 65.00, 35);
    v_ite := novo_item(v_con, 'Recurso de multa gravissima', v_mul);
    nova_instancia(v_ite, 'AUTUACAO', 'PROTOCOLADO', NULL, 20);
    v_ite := novo_item(v_con, 'Recurso de multa grave', v_mul2);
    nova_instancia(v_ite, 'AUTUACAO', 'AGUARDANDO_JULGAMENTO', NULL, 18);
    v_ite := novo_item(v_con, 'Recurso de multa media', v_mul3);
    nova_instancia(v_ite, 'AUTUACAO', 'AGUARDANDO_DOCUMENTO', 6);
    novo_pagamento(v_con, 125.00, 'PIX', 30);
    novo_pagamento(v_con, 125.00, 'PIX', 5);
    nova_despesa(v_con, 'Correio - tres AR', 55.50, 28);
    novo_compromisso(6, 'PRAZO', 'Protocolar o terceiro recurso - AIT TST000032', v_cli, '11:00');
    novo_compromisso(-3, 'ATENDIMENTO', 'Reuniao para fechar o pacote', v_cli, '15:30');

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('OK: 30 clientes de teste criados.');

EXCEPTION
    -- Sem isto, um erro no meio dos 30 desfaz tudo e a mensagem nao diz onde parou —
    -- e como a limpeza la em cima ja foi confirmada, o banco fica vazio.
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE_APPLICATION_ERROR(
            -20001,
            'Falhou em: ' || v_etapa || ' >>> ' || SQLERRM);
END;
/


-- ---------------------------------------------------------------------
-- 3. Confere o que entrou
-- ---------------------------------------------------------------------
SELECT 'clientes'      AS TABELA, COUNT(*) AS QUANTIDADE FROM CLIENTES WHERE CPF LIKE '900000000%'
UNION ALL
SELECT 'veiculos',     COUNT(*) FROM VEICULOS WHERE PLACA LIKE 'TST%'
UNION ALL
SELECT 'multas',       COUNT(*) FROM MULTAS   WHERE AIT LIKE 'TST%'
UNION ALL
SELECT 'processos cnh', COUNT(*) FROM PROCESSOS_CNH WHERE NUMERO_PROCESSO LIKE 'TST%'
UNION ALL
SELECT 'contratacoes', COUNT(*) FROM CONTRATACOES
 WHERE CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%')
UNION ALL
SELECT 'instancias',   COUNT(*) FROM INSTANCIAS I
  JOIN ITENS_CONTRATACAO IT ON IT.ID = I.ITEM_ID
  JOIN CONTRATACOES C ON C.ID = IT.CONTRATACAO_ID
 WHERE C.CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%')
UNION ALL
SELECT 'pagamentos',   COUNT(*) FROM PAGAMENTOS P
  JOIN CONTRATACOES C ON C.ID = P.CONTRATACAO_ID
 WHERE C.CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%')
UNION ALL
SELECT 'compromissos', COUNT(*) FROM AGENDA
 WHERE CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%');

-- Deve sobrar em aberto, com prazo e arquivados para as abas da lista.
SELECT COUNT(*) AS COM_VALOR_EM_ABERTO FROM VW_COBRANCAS_ABERTAS
 WHERE CLIENTE_ID IN (SELECT ID FROM CLIENTES WHERE CPF LIKE '900000000%');
