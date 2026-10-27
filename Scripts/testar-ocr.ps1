<#
    Le um documento com o OCR do proprio Windows e mostra o texto cru.

    Serve para decidir, antes de codar, se o OCR local da conta de importar
    os dados de uma CNH. O motor usado aqui e exatamente o mesmo que o
    sistema usaria (Windows.Media.Ocr), entao o que sair aqui e o que o
    programa teria para trabalhar.

    USO

        .\Scripts\testar-ocr.ps1 "C:\caminho\da\foto.jpg"

    O que olhar na saida: se NOME, CPF, DOC. IDENTIDADE, Nº REGISTRO,
    CAT. HAB. e VALIDADE sairam legiveis e na mesma linha do rotulo.
    E isso que torna possivel achar cada campo pelo rotulo.
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$Arquivo
)
$ErrorActionPreference = "Stop"

# As classes WinRT so aparecem depois de referenciadas assim.
[Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime]              | Out-Null
[Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType = WindowsRuntime] | Out-Null
[Windows.Media.Ocr.OcrEngine, Windows.Media.Ocr, ContentType = WindowsRuntime]            | Out-Null
[Windows.Globalization.Language, Windows.Globalization, ContentType = WindowsRuntime]      | Out-Null
Add-Type -AssemblyName System.Runtime.WindowsRuntime

# WinRT devolve IAsyncOperation; o PowerShell nao sabe esperar sozinho.
$comoTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and
    $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1'
})[0]

function Esperar($operacao, $tipo) {
    $task = $comoTask.MakeGenericMethod($tipo).Invoke($null, @($operacao))
    $task.Wait(-1) | Out-Null
    $task.Result
}

$caminho = (Resolve-Path $Arquivo).Path

$motor = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage([Windows.Globalization.Language]::new("pt-BR"))
if ($null -eq $motor) { $motor = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages() }
if ($null -eq $motor) { throw "Nenhum idioma de OCR instalado no Windows." }

$arq       = Esperar ([Windows.Storage.StorageFile]::GetFileFromPathAsync($caminho)) ([Windows.Storage.StorageFile])
$fluxo     = Esperar ($arq.OpenAsync([Windows.Storage.FileAccessMode]::Read))        ([Windows.Storage.Streams.IRandomAccessStream])
$decodific = Esperar ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($fluxo)) ([Windows.Graphics.Imaging.BitmapDecoder])
$imagem    = Esperar ($decodific.GetSoftwareBitmapAsync())                          ([Windows.Graphics.Imaging.SoftwareBitmap])

Write-Output ""
Write-Output "  Arquivo : $(Split-Path -Leaf $caminho)"
Write-Output "  Imagem  : $($imagem.PixelWidth) x $($imagem.PixelHeight) px  (limite do motor: $([Windows.Media.Ocr.OcrEngine]::MaxImageDimension) px)"
Write-Output "  Idioma  : $($motor.RecognizerLanguage.LanguageTag)"

if ($imagem.PixelWidth  -gt [Windows.Media.Ocr.OcrEngine]::MaxImageDimension -or
    $imagem.PixelHeight -gt [Windows.Media.Ocr.OcrEngine]::MaxImageDimension) {
    throw "Imagem maior que o limite do motor. Reduza a foto antes de tentar."
}

$resultado = Esperar ($motor.RecognizeAsync($imagem)) ([Windows.Media.Ocr.OcrResult])

Write-Output ""
Write-Output "  ---------- LINHAS, como o motor agrupou ----------"
Write-Output ""

$n = 0
foreach ($linha in $resultado.Lines) {
    $n++
    Write-Output ("  {0,3}  {1}" -f $n, $linha.Text)
}

Write-Output ""
Write-Output "  Total: $n linhas, $(($resultado.Lines | ForEach-Object { $_.Words.Count } | Measure-Object -Sum).Sum) palavras"
Write-Output ""
