$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$target=Join-Path $root '.tools/gitleaks'
New-Item -ItemType Directory -Force $target | Out-Null
$zip=Join-Path $target 'gitleaks.zip'
Invoke-WebRequest 'https://github.com/gitleaks/gitleaks/releases/download/v8.30.1/gitleaks_8.30.1_windows_x64.zip' -OutFile $zip
$expected='D29144DEFF3A68AA93CED33DDDF84B7FDC26070ADD4AA0F4513094C8332AFC4E'
if((Get-FileHash $zip -Algorithm SHA256).Hash -ne $expected){throw 'Checksum de Gitleaks inválido.'}
Expand-Archive $zip $target -Force
& (Join-Path $target 'gitleaks.exe') version
if($LASTEXITCODE -ne 0){throw 'No se pudo ejecutar Gitleaks.'}
