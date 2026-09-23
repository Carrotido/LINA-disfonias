[CmdletBinding()]
param(
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$exportDirectory = Join-Path $projectRoot 'build\windows'
$exportPath = Join-Path $exportDirectory 'VocalisFonoPlay.exe'
$presetName = 'Windows Desktop'

# Set GODOT_PATH to use a different Godot installation without editing this file.
$godotCandidates = @(
    $env:GODOT_PATH,
    'C:\Users\matyb\AppData\Local\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe',
    'C:\Program Files\Godot\Godot.exe'
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

$godotPath = $godotCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $godotPath) {
    throw 'No se encontró Godot .NET. Definí la variable GODOT_PATH con la ruta al ejecutable de Godot.'
}

New-Item -ItemType Directory -Force -Path $exportDirectory | Out-Null

Write-Host 'Exportando Vocalis FonoPlay (Windows Desktop, Debug)...' -ForegroundColor Cyan
& $godotPath --headless --path $projectRoot --export-debug $presetName $exportPath
if ($LASTEXITCODE -ne 0) {
    throw "La exportación falló con código $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $exportPath)) {
    throw 'Godot finalizó, pero no se encontró el ejecutable exportado.'
}

Write-Host "Exportación lista: $exportPath" -ForegroundColor Green
if (-not $NoLaunch) {
    Start-Process -FilePath $exportPath -WorkingDirectory $exportDirectory
}
