# Contexto para o Claude Code

## O projeto

Sistema de gestão para um escritório de **recursos de trânsito**: clientes, recursos protocolados e, principalmente, o **controle financeiro** (quem contratou o quê, o que foi pago, o que está em aberto). É um sistema de uso real, não um exercício de portfólio.

Nasceu de um clone do [GerenciadorClientes](https://github.com/GustavoFranca7/GerenciadorClientes), que trouxe pronto o CRUD de clientes e endereços. Dele não sobrou nada em uso: as tabelas foram remodeladas e a interface em pixel art saiu do código em 08/10/2026, junto com as telas antigas (`MainWindow`, `FormularioCliente`, `ConfirmacaoExclusao`), os controles próprios e as artes.

**Próximos passos combinados:**

1. ~~Modelar as tabelas novas (recursos e financeiro)~~ — feito
2. ~~Trocar a arte em pixel art por um visual formal~~ — feito
3. Telas de recursos, pagamentos e relatórios

## Como trabalhar aqui

- **Sempre responder em português do Brasil.**
- **Comentar o código**, de forma curta e direta: no máximo 1 ou 2 linhas por bloco, explicando o "porquê". Explicações longas vão na conversa, não no arquivo.
- **Nunca fazer commit ou push sem o usuário pedir.** Quando pedir, escrever a mensagem em português e **sem** os trailers `Co-Authored-By` ou `Claude-Session`: os commits são dele.
- Reescrever histórico ou usar `--force` só com autorização explícita.
- Antes de dar uma mudança por pronta, **compilar** e, quando mexer na interface, **rodar o app** e conferir na tela.

## Estrutura

```
GestorRecursos.Core/         Regras e acesso ao banco (não conhece nenhuma tela)
├── Data/                    Database (conexão async), DAOs e extensões de leitura
├── Models/                  Entidades e os enums dos domínios fechados
└── Validacao/               Normalização de CPF, telefone, UF e CEP

GestorRecursos.Api/          API REST em ASP.NET Core (consome a Core direto)
GestorRecursos.Desktop/      Interface gráfica (Avalonia 12 + CommunityToolkit.Mvvm)
├── Assets/Icones.axaml      Os desenhos dos ícones, por nome
├── Styles/
│   ├── Cores.axaml          A paleta: toda cor do sistema sai daqui
│   └── Componentes.axaml    O que mais de uma tela usa (dado copiável, busca, selo, ação)
├── Servicos/                Área de transferência e máscaras de digitação
├── Views/                   Telas em XAML, por área
│   ├── Janela/              A moldura: menu lateral e barra de cima
│   ├── Clientes/            Listagem, ficha e cadastro/edição
│   └── Arquivados/          O arquivo
└── ViewModels/              Estado e comandos, nas mesmas pastas das Views
    └── Comum/               Base, formatação, prazos e conversores

Database/                    Scripts numerados: 01 a 04 montam o banco; 90 e 99 sao utilitarios
```

**Cada tela é um par.** `Views/Clientes/FichaClienteView.axaml` ↔
`ViewModels/Clientes/FichaClienteViewModel.cs`: mesmo nome, mesma pasta. O `ViewLocator`
liga os dois trocando "ViewModel" por "View" no nome do tipo, então renomear um obriga a
renomear o outro. As pastas são arrumação: o namespace continua um por camada
(`GestorRecursos.Desktop.Views` e `...ViewModels`).

A dependência aponta em um sentido só: as interfaces usam a `Core`, e a `Core` não conhece nenhuma delas. Nada de SQL ou de acesso ao Oracle fora da `Core/Data`.

## Banco de dados

- Oracle (XE local), acessado com `Oracle.ManagedDataAccess.Core`.
- **Nenhuma credencial no código.** A connection string `Oracle` vem do `appsettings.json`, dos User Secrets ou de variáveis de ambiente (veja o README).
- Sempre **consultas parametrizadas** (`:nome`, `:cpf`...), nunca concatenação.
- Gravações que envolvem mais de uma tabela vão em **transação**, com `Rollback` em caso de erro.
- **Não rodar DDL no banco por conta própria.** Quando precisar de uma tabela ou coluna nova, escrever o script, explicar e deixar que o usuário execute.

## Convenções de código

- Código, nomes e comentários em português (sem acentos nos identificadores).
- Avalonia com MVVM: a tela não conversa com o banco; ela exibe o que está no ViewModel e dispara comandos.
- Nos ViewModels, `[ObservableProperty]` e `[RelayCommand]` do CommunityToolkit.
- Cores sempre pelos nomes definidos em `Styles/Cores.axaml`; nada de código hexadecimal solto no XAML.
- Estilo que serve a mais de uma tela mora em `Styles/Componentes.axaml`, nunca copiado nas duas.
- **Ação concluída avisa pela `Servicos.Notificacoes`**, nunca com uma tarja nova na tela. O
  aviso aparece no canto inferior direito, fica 3 segundos e se apaga sozinho — vale para
  copiar, salvar, importar, anexar, apagar e o que vier. Fica **na tela** só o que precisa
  ser lido com calma, copiado ou corrigido: senha provisória, código de recuperação, lista
  do que não entrou. Mensagem de erro também fica, até a pessoa resolver.

## Rodar e testar

```bash
dotnet build GestorRecursos.slnx
dotnet run --project GestorRecursos.Desktop
```

Se o build falhar dizendo que um arquivo está em uso, é a pré-visualização do Rider segurando a DLL. Compile numa pasta separada:

```bash
dotnet build GestorRecursos.Desktop -p:BaseOutputPath=<pasta-temporaria>/
```

Para testar com dados, criar registros de teste à vontade, desde que o **nome comece com "TESTE "** e o **CPF comece com 900000000**. Os demais registros do banco são reais: não alterar nem excluir.
