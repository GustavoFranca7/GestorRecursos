# Banco na nuvem (Oracle Autonomous Database — Always Free)

Passo a passo para criar o banco do sistema na nuvem da Oracle, de graça, e apontar o programa para ele.

## 1. Criar a conta

1. Acesse **oracle.com/cloud/free** e clique em *Start for free*.
2. Preencha os dados. A Oracle pede um **cartão de crédito só para validar identidade** — o Always Free não cobra. Uma cobrança de teste de poucos centavos pode aparecer e é estornada.
3. **Escolha a região com cuidado: ela não muda depois.** Use `Brazil East (Sao Paulo)` ou `Brazil Southeast (Vinhedo)`, que ficam perto e deixam o sistema mais rápido.
4. Confirme o e-mail e entre no painel (Console da OCI).

## 2. Criar o banco

1. No menu (canto superior esquerdo): **Oracle Database → Autonomous Database**.
2. Botão **Create Autonomous Database**.
3. Preencha:
   - *Display name* e *Database name*: `gestorrecursos`
   - *Workload type*: **Transaction Processing**
   - **Marque `Always Free`** (aparece um seletor no topo; sem ele a conta passa a ser cobrada)
   - *Database version*: a mais recente oferecida
   - *Password*: senha do usuário **ADMIN** — 12 a 30 caracteres, com maiúscula, minúscula e número. Guarde no gerenciador de senhas.
4. Em **Network access**, escolha **Secure access from everywhere**.
   Com essa opção a Oracle **exige mTLS**, ou seja, a conexão usa uma carteira de certificados (*wallet*).
   O botão de desligar o mTLS fica apagado até existir uma ACL (lista de IPs autorizados) ou um endpoint privado — veja a nota no fim deste arquivo.
5. Clique em **Create**. Em 1 a 2 minutos o banco fica *Available*.

## 3. Criar o usuário da aplicação

Nunca use o ADMIN no programa: ele é dono de tudo no banco.

1. Na tela do banco, clique em **Database actions → SQL** e entre como `ADMIN`.
2. Abra o arquivo `UsuarioNuvem.sql` deste projeto, **troque a senha** e execute.
3. Saia e entre de novo em **Database actions → SQL**, agora com o usuário `GESTOR`.
4. Execute o `CriacaoBanco.sql` inteiro. Ao final, confira:

   ```sql
   SELECT table_name FROM user_tables ORDER BY table_name;
   SELECT nome FROM categorias_servico;
   ```

   Devem aparecer 7 tabelas e os 8 serviços.

## 4. Baixar a wallet

1. Na tela do banco, clique em **Database connection**.
2. Em *Download client credentials (Wallet)*, clique em **Download wallet**.
3. Defina uma senha para a wallet (guarde no gerenciador de senhas) e baixe o `Wallet_GESTORRECURSOS.zip`.
4. Descompacte numa pasta **fora do repositório**, por exemplo:
   `C:\Users\<seu-usuario>\oracle\wallet-gestorrecursos`

   A pasta terá `tnsnames.ora`, `sqlnet.ora`, `cwallet.sso` e outros arquivos.
   **Nunca coloque essa pasta dentro do projeto nem no Git**: quem tiver a wallet e a senha do banco entra nele.

5. Abra o `tnsnames.ora` para ver os nomes de conexão disponíveis. Use o que termina em **`_tp`**
   (*transaction processing*, o perfil normal de uso), por exemplo `gestorrecursos_tp`.

## 5. Apontar o programa para a nuvem

Na pasta do projeto `GestorRecursos.Core`, rode (tudo numa linha só, trocando a senha e o caminho):

```bash
dotnet user-secrets set "ConnectionStrings:Oracle" "User Id=GESTOR;Password=SUA_SENHA;Data Source=gestorrecursos_tp;TnsAdmin=C:\Users\SEU_USUARIO\oracle\wallet-gestorrecursos;WalletLocation=C:\Users\SEU_USUARIO\oracle\wallet-gestorrecursos;"
```

- `Data Source` é o nome que está no `tnsnames.ora`, não o endereço do servidor.
- `TnsAdmin` e `WalletLocation` apontam para a pasta onde a wallet foi descompactada.
- A senha continua fora do código e fora do Git, como já era com o banco local.

> **Atenção (corrigido em 30/09).** O driver gerenciado (`Oracle.ManagedDataAccess.Core`)
> **recusa** `TnsAdmin` e `WalletLocation` dentro da connection string — devolve
> `ORA-50008: 'TnsAdmin' is an invalid connection string attribute`. Esses atributos existiam
> no driver antigo, e a documentação da Oracle mistura os dois formatos.
> No driver gerenciado eles são propriedades estáticas (`OracleConfiguration.TnsAdmin`).
>
> A `Database` da Core resolve isso: aceita os dois atributos na string, retira-os e aplica
> em `OracleConfiguration` antes de conectar. Então o comando acima **funciona como está** —
> mas se um dia alguém montar a conexão sem passar pela `Database`, vai esbarrar nisso.
>
> Também dá para configurar sem sujar a string, por chave própria:
> `dotnet user-secrets set "Oracle:TnsAdmin" "D:\caminho\da\wallet"`

> Para rodar em outro computador (o notebook dela, por exemplo), basta copiar a pasta da wallet
> para lá e repetir esse comando com o caminho daquele computador.

## 6. Cuidados com o Always Free

- O banco **para sozinho depois de 7 dias sem nenhuma conexão**. Os dados ficam guardados; basta clicar em *Start* no console. Com uso diário, não acontece.
- Se ficar **90 dias parado**, a Oracle pode apagar. Por isso, backup é obrigatório.
- Limites: 20 GB, 1 OCPU e 20 conexões ao mesmo tempo — folgado para o escritório.

## 7. Backup

Enquanto não houver rotina automática, uma vez por semana:

1. **Database actions → Data Load → Export** (ou *Data Pump*), exportando o schema `GESTOR`.
2. Guardar o arquivo no computador **e** em nuvem pessoal (Drive, OneDrive).

**No Always Free não existe restauração.** Conferido na documentação da Oracle em 29/10/2026: a instância gratuita não aceita backup manual, não aceita backup de longo prazo e **não aceita restaurar** — nem para um backup anterior, nem para um instante no tempo. Para ter isso, seria preciso virar instância paga.

Ou seja: **o arquivo exportado não é a segunda linha de defesa, é a única.** Se a Oracle guarda algum backup interno, ele não está ao nosso alcance.

> O desenho da rotina **automática** que vai substituir este passo a passo manual,
> com os tamanhos medidos e o procedimento de restauração, está na documentação:
> `Documentacao/1 Projeto/Rotina de backup.md`.

## 8. Nota sobre conectar sem wallet

Com *Access type* = **Allow secure access from everywhere**, a Oracle deixa o mTLS obrigatório e o botão
de editar fica apagado. Para conectar só com usuário e senha seria preciso:

1. Em **Network → Access control list**, clicar em **Edit**, marcar *Enable access control list* e
   cadastrar o IP público de quem vai conectar; **depois disso** o botão do mTLS libera e ele pode virar
   *Not required*.

O problema é que a maioria das conexões domésticas tem **IP dinâmico**: quando o IP muda, o acesso é
bloqueado até alguém atualizar a lista no painel. Por isso o caminho escolhido aqui foi a wallet, que
funciona de qualquer rede.
