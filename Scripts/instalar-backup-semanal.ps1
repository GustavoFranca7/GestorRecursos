<#
    Publica o backup e registra a tarefa semanal no Agendador do Windows.

    Roda UMA VEZ, nesta maquina. Depois disso o Windows cuida sozinho.

    O QUE ELE FAZ

    1. Publica o GestorRecursos.Backup em Release, numa pasta propria dentro
       do perfil do usuario.
    2. Registra uma tarefa semanal que chama backup-semanal.ps1.
    3. Roda o backup uma vez, na hora, para conferir que funciona.

    Release importa: em Debug a Core tambem le o cofre local de segredos, que
    aponta para o banco de TESTE. Publicado em Release, o backup enxerga apenas
    a configuracao que o script passa - a de producao.

    SOBRE A JANELA PERDIDA

    A tarefa roda com "-StartWhenAvailable": se o computador estiver desligado
    na hora marcada, ela roda assim que ligar. Sem isso, um domingo de maquina
    desligada significaria uma semana sem backup, em silencio.

    USO

        .\Scripts\instalar-backup-semanal.ps1 -Destino "D:\Meu Drive\Backups Gestor"
        .\Scripts\instalar-backup-semanal.ps1 -Destino "..." -Dia Sexta -Hora 20:00
#>
param(
    # Pasta onde o .zip fica. Use uma sincronizada com Drive ou OneDrive.
    [Parameter(Mandatory = $true)]
    [string]$Destino,

    [ValidateSet("Domingo","Segunda","Terca","Quarta","Quinta","Sexta","Sabado")]
    [string]$Dia = "Domingo",

    [string]$Hora = "20:00",

    [string]$Tarefa = "Gestor de Recursos - backup semanal"
)

$ErrorActionPreference = "Stop"

$raiz     = Split-Path -Parent $PSScriptRoot
$projeto  = Join-Path $raiz "GestorRecursos.Backup"
$programa = Join-Path $env:LOCALAPPDATA "Programs\GestorRecursos-Backup"
$roteiro  = Join-Path $PSScriptRoot "backup-semanal.ps1"

$diasDoWindows = @{
    Domingo = "Sunday"; Segunda = "Monday"; Terca = "Tuesday"; Quarta = "Wednesday"
    Quinta  = "Thursday"; Sexta = "Friday"; Sabado = "Saturday"
}

Write-Output ""
Write-Output "1. Publicando o backup em Release..."

dotnet publish $projeto -c Release -o $programa --nologo
if ($LASTEXITCODE -ne 0) { throw "A publicacao falhou. A tarefa nao foi registrada." }

Write-Output "   -> $programa"

Write-Output ""
Write-Output "2. Registrando a tarefa semanal..."

# -NoProfile para a tarefa nao depender do perfil do PowerShell do usuario, e
# -ExecutionPolicy Bypass porque a politica padrao do Windows recusa script
# em arquivo. Nada disso afrouxa a maquina: vale so para esta chamada.
$acao = New-ScheduledTaskAction `
    -Execute "powershell.exe" `
    -Argument "-NoProfile -ExecutionPolicy Bypass -File `"$roteiro`" -Destino `"$Destino`""

$gatilho = New-ScheduledTaskTrigger `
    -Weekly `
    -DaysOfWeek $diasDoWindows[$Dia] `
    -At $Hora

# StartWhenAvailable: domingo com o computador desligado nao perde a semana - a tarefa
# roda assim que ele ligar. Restart: queda de internet no horario tambem nao perde, porque
# o banco esta na nuvem e a falha costuma durar minutos.
$opcoes = New-ScheduledTaskSettingsSet `
    -StartWhenAvailable `
    -DontStopIfGoingOnBatteries `
    -AllowStartIfOnBatteries `
    -RestartCount 3 `
    -RestartInterval (New-TimeSpan -Minutes 15) `
    -ExecutionTimeLimit (New-TimeSpan -Hours 1)

Register-ScheduledTask `
    -TaskName $Tarefa `
    -Action $acao `
    -Trigger $gatilho `
    -Settings $opcoes `
    -Description "Exporta o banco real para um .zip em $Destino e copia para la os documentos anexados que ainda nao estao na pasta. O Always Free nao tem restauracao: estas sao as unicas copias." `
    -Force | Out-Null

Write-Output "   -> $Dia, $Hora, semanalmente"

Write-Output ""
Write-Output "3. Rodando uma vez agora, para conferir..."
Write-Output ""

& $roteiro -Destino $Destino -Programa $programa

if ($LASTEXITCODE -ne 0) {
    Write-Output ""
    Write-Output "  A tarefa esta registrada, mas esta primeira execucao falhou."
    Write-Output "  Corrija o motivo acima e rode: .\Scripts\backup-semanal.ps1 -Destino `"$Destino`""
    exit 1
}

Write-Output "Pronto. Confira em: Agendador de Tarefas > Biblioteca > `"$Tarefa`""
Write-Output ""
