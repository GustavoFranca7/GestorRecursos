<#
    Publica o desktop num formato que roda na maquina dela sem instalar nada.

    Autossuficiente e em arquivo unico de proposito: medido em 21/10, sai menor
    (124 MB, 2 arquivos) do que a publicacao que depende do runtime (154 MB, 60
    arquivos) e dispensa instalar o .NET 10 na maquina dela.

    Uso:
        .\publicar.ps1              # versao 1.0.0
        .\publicar.ps1 -Versao 1.1.0
#>
param(
    [string]$Versao = "1.0.0"
)

$ErrorActionPreference = "Stop"

$raiz     = Split-Path -Parent $PSScriptRoot
$projeto  = Join-Path $raiz "GestorRecursos.Desktop"
$saida    = Join-Path $PSScriptRoot "saida\app"

if (Test-Path $saida) { Remove-Item $saida -Recurse -Force }

Write-Output "Publicando $Versao..."

dotnet publish $projeto `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:DebugType=none `
    -p:Version=$Versao `
    -o $saida

if ($LASTEXITCODE -ne 0) { throw "A publicacao falhou." }

# O .pdb nao e erro de build, mas nao tem o que fazer na maquina dela.
Get-ChildItem $saida -Filter *.pdb -ErrorAction SilentlyContinue | Remove-Item -Force

$total = (Get-ChildItem $saida -Recurse | Measure-Object -Property Length -Sum).Sum
Write-Output ""
Write-Output "Pronto: $saida"
Write-Output ("Tamanho: {0:N0} MB em {1} arquivo(s)" -f ($total / 1MB), (Get-ChildItem $saida).Count)
Write-Output ""
Write-Output "Falta a carga: appsettings.json e a pasta da wallet em Instalador\carga\."
Write-Output "Veja Instalador\LEIA-ME.md."
