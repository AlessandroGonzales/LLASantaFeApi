# Instalación interactiva: Windows puede solicitar elevación mediante UAC.
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$installer = Join-Path $taskRoot '.tools/vs2026/vs_community.exe'
if (-not (Test-Path $installer)) { throw 'Falta el instalador preparado en .tools/vs2026.' }
$signature = Get-AuthenticodeSignature $installer
if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
    throw 'El instalador no tiene una firma válida de Microsoft.'
}
Start-Process -FilePath $installer -ArgumentList @('--installPath', '"C:\Program Files\Microsoft Visual Studio\2026\Community"', '--add', 'Microsoft.VisualStudio.Workload.NetWeb', '--includeRecommended') -Wait
