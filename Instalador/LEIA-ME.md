# Instalador do Gestor de Recursos

Gera um `.exe` único que instala o sistema na máquina dela: atalho no menu Iniciar,
desinstalador, e nada de .NET para instalar antes.

> **O arquivo gerado é um segredo.** Ele carrega dentro a senha do banco e a wallet do
> Oracle. Não pode ir para o Git, nem para o Drive, nem por e-mail sem cuidado. Quem gera
> o instalador é quem tem a wallet — hoje, só você.

## O que é preciso ter, uma vez

1. **Inno Setup** (gratuito) — <https://jrsoftware.org/isdl.php>. Instale e deixe o
   `ISCC.exe` acessível (fica em `C:\Program Files (x86)\Inno Setup 6\`).
2. **A wallet** do Autonomous Database, descompactada. É a mesma de
   `Database/ConfiguracaoNuvem.md`, passo 4.
3. **A senha** do usuário de banco que vai na máquina dela.

## A carga

A pasta `carga/` não é versionada. Monte assim:

```
Instalador/
  carga/
    appsettings.json     <- copie de appsettings.exemplo.json e preencha
    wallet/              <- o conteudo do Wallet_GESTORRECURSOS.zip
      cwallet.sso
      tnsnames.ora
      sqlnet.ora
      ...
```

O `TnsAdmin` do `appsettings.json` fica em `wallet` — caminho relativo, que o programa
resolve a partir da própria pasta de instalação. Assim o instalador funciona em qualquer
pasta, sem caminho fixo.

## Gerar

```powershell
# 1. publica o programa (sai em saida\app)
.\publicar.ps1 -Versao 1.0.0

# 2. monta o instalador (sai em saida\GestorRecursos-1.0.0-setup.exe)
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" /DVersao=1.0.0 GestorRecursos.iss
```

## Decisões por trás disto

**Autossuficiente, arquivo único.** Medido em 21/10: 124 MB em 2 arquivos, contra 154 MB
em 60 arquivos da publicação que depende do runtime — menor *e* dispensa instalar o .NET
10 na máquina dela.

**Instalação no perfil do usuário**, e não em Arquivos de Programas. Não pede senha de
administrador: ela não tem por que digitar uma para usar o sistema.

**Inno Setup, e não MSIX nem Velopack.** O MSIX exige certificado de assinatura fora da
Loja. O Velopack é melhor — faz atualização automática —, mas precisa de um lugar para
hospedar o feed de versões, que é outra decisão. Com uma usuária, entregar um `.exe` novo
resolve, e trocar para o Velopack depois não joga este trabalho fora.

## O que ainda falta

- **Ícone.** O programa não tem um `.ico`, então o atalho usa o ícone genérico do Windows.
- **Atualização.** Hoje é gerar um instalador novo e ela rodar por cima.
- **A senha em texto.** Fica no `appsettings.json` da máquina dela. O jeito de acabar com
  isso é o desktop parar de falar com o banco e passar a falar com a API (RNF-04) — fase
  própria, decidida para depois da entrega.
