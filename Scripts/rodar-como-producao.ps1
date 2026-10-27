<#
    Roda o sistema pelo codigo-fonte, mas apontando para o banco REAL.

    Serve para conferir algo que so acontece na producao - um dado que ela
    relatou, um caso que o banco de teste nao tem. O uso normal continua
    sendo o Rider, que aponta para o teste.

    COMO FUNCIONA

    Le a configuracao do programa ja instalado e a passa por variavel de
    ambiente, que vence o cofre local na ordem de leitura. Nada e gravado:
    quando este terminal fechar, acabou. Nao ha o que lembrar de desfazer.

    Nenhuma credencial nova entra no repositorio - o que ele usa ja esta na
    pasta de instalacao.

    A tela mostra PRODUCAO - DADOS REAIS enquanto estiver assim.

    USO

        .\Scripts\rodar-como-producao.ps1
#>
$ErrorActionPreference = "Stop"

$instalado = Join-Path $env:LOCALAPPDATA "Programs\GestorRecursos"
$config    = Join-Path $instalado "appsettings.json"

if (-not (Test-Path $config)) {
    throw "Nao achei o programa instalado em $instalado. " +
          "Instale o sistema nesta maquina antes de usar este script."
}

$c = Get-Content $config -Raw | ConvertFrom-Json

# A wallet do instalador fica ao lado do executavel, por caminho relativo.
# Daqui o programa roda de outra pasta, entao o caminho vai absoluto.
$wallet = Join-Path $instalado $c.Oracle.TnsAdmin

$env:ConnectionStrings__Oracle = $c.ConnectionStrings.Oracle
$env:Oracle__TnsAdmin          = $wallet
$env:Oracle__Esquema           = $c.Oracle.Esquema
$env:Ambiente                  = "PRODUCAO - DADOS REAIS"

if ($c.Email) {
    $env:Email__Servidor = $c.Email.Servidor
    $env:Email__Porta    = $c.Email.Porta
    $env:Email__Usuario  = $c.Email.Usuario
    $env:Email__Senha    = $c.Email.Senha
}

Write-Output ""
Write-Output "  ATENCAO: apontando para o BANCO REAL."
Write-Output "  Usuario do banco: $($c.ConnectionStrings.Oracle -replace 'Password=[^;]*','Password=***')"
Write-Output "  Wallet: $wallet"
Write-Output "  Isto vale so para esta execucao."
Write-Output ""

$raiz = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $raiz "GestorRecursos.Desktop")
