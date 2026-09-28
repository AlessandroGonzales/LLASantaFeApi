#Requires -Version 7.2
param([Parameter(Mandatory)][string]$ClamDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$clamRoot = (Resolve-Path -LiteralPath $ClamDirectory).Path
$freshclam = Join-Path $clamRoot 'freshclam.exe'
$clamd = Join-Path $clamRoot 'clamd.exe'
if (-not (Test-Path -LiteralPath $freshclam) -or -not (Test-Path -LiteralPath $clamd)) {
    throw 'ClamDirectory debe ser la carpeta del ZIP oficial que contiene freshclam.exe y clamd.exe.'
}
$dataRoot = Join-Path $taskRoot '.artifacts/clamav'
$database = Join-Path $dataRoot 'database'
New-Item -ItemType Directory -Force $database | Out-Null
$freshConfig = Join-Path $dataRoot 'freshclam.conf'
$clamConfig = Join-Path $dataRoot 'clamd.conf'
@"
DatabaseDirectory "$database"
DatabaseMirror database.clamav.net
"@ | Set-Content -LiteralPath $freshConfig -Encoding utf8NoBOM
$limits = Get-Content (Join-Path $taskRoot 'deployment/clamd-limits.conf.example') -Raw
($limits + [Environment]::NewLine + "DatabaseDirectory `"$database`"" + [Environment]::NewLine + 'Foreground yes') |
    Set-Content -LiteralPath $clamConfig -Encoding utf8NoBOM
Write-Host 'Actualizando firmas oficiales de ClamAV. La primera descarga puede tardar.'
& $freshclam "--config-file=$freshConfig"
if ($LASTEXITCODE -ne 0) { throw 'FreshClam no pudo actualizar las firmas. Revisá el error anterior antes de iniciar clamd.' }
Write-Host 'Iniciando clamd en 127.0.0.1:3310. Dejá esta terminal abierta mientras probás PDF. Ctrl+C para detener.'
& $clamd "--config-file=$clamConfig"
if ($LASTEXITCODE -ne 0) { throw 'clamd terminó con error. Revisá el diagnóstico anterior.' }
