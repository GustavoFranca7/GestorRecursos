<#
    Roda o backup do banco REAL numa pasta sincronizada: o .zip do texto e os
    documentos anexados.

    Quem chama isto e o Agendador de Tarefas do Windows, uma vez por semana.
    Para registrar a tarefa, use instalar-backup-semanal.ps1 (uma vez so).

    POR QUE ELE EXISTE

    O Always Free nao tem restauracao: nao aceita backup manual, nao aceita
    backup de longo prazo e nao aceita restaurar. O que isto gera nao e a
    segunda linha de defesa, e a unica.

    SAO DUAS COISAS NA MESMA PASTA

    O .zip tem o texto do banco e restaura o sistema inteiro, menos os
    documentos - o .sql nao carrega BLOB. Por isso a subpasta "Documentos dos
    clientes", que recebe os PDFs e DOCX anexados. Ela e incremental: cada
    semana copia so o que entrou, porque gravar o acervo inteiro toda semana
    encheria o Drive de repeticao.

    Ver "Documentacao/1 Projeto/Rotina de backup.md".

    COMO FUNCIONA

    Le a configuracao do programa ja instalado e a passa por variavel de
    ambiente, igual ao rodar-como-producao.ps1. Nenhuma credencial e copiada,
    nem gravada na tarefa agendada: ela continua existindo em um lugar so,
    dentro da pasta de instalacao.

    O backup em si nao vem no repositorio compilado - instalar-backup-semanal
    publica o executavel antes de registrar a tarefa.

    USO

        .\Scripts\backup-semanal.ps1
        .\Scripts\backup-semanal.ps1 -Destino "D:\Meu Drive\Backups Gestor"
#>
param(
    # Onde o .zip vai parar. O ideal e uma pasta sincronizada com Drive ou
    # OneDrive: assim o backup sai do computador sozinho, e um disco que morre
    # nao leva a unica copia com ele.
    [string]$Destino,

    # Onde o executavel do backup foi publicado.
    [string]$Programa = (Join-Path $env:LOCALAPPDATA "Programs\GestorRecursos-Backup")
)

$ErrorActionPreference = "Stop"

$instalado = Join-Path $env:LOCALAPPDATA "Programs\GestorRecursos"
$config    = Join-Path $instalado "appsettings.json"
$exe       = Join-Path $Programa "GestorRecursos.Backup.exe"

if (-not (Test-Path $config)) {
    throw "Nao achei o programa instalado em $instalado. A configuracao do banco real sai de la."
}

if (-not (Test-Path $exe)) {
    throw "Nao achei o executavel do backup em $exe. Rode instalar-backup-semanal.ps1 primeiro."
}

$c = Get-Content $config -Raw | ConvertFrom-Json

# A wallet do instalador fica ao lado do executavel do sistema, por caminho
# relativo. Daqui o backup roda de outra pasta, entao o caminho vai absoluto.
$wallet = Join-Path $instalado $c.Oracle.TnsAdmin

$env:ConnectionStrings__Oracle = $c.ConnectionStrings.Oracle
$env:Oracle__TnsAdmin          = $wallet
$env:Oracle__Esquema           = $c.Oracle.Esquema

# Vazio de proposito: producao nao se anuncia. O proprio backup imprime o nome
# do banco que alcancou, lido do banco - e assim um backup feito contra o teste
# por engano se denuncia no arquivo.
$env:Ambiente = ""

if ($Destino) {
    $env:Backup__Pasta = $Destino
}

& $exe
exit $LASTEXITCODE
