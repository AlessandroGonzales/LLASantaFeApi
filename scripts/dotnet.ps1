# Use the workspace SDK when present; otherwise global.json selects the installed SDK.
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$localDotnet = Join-Path $taskRoot '.tools/dotnet/dotnet.exe'
if (Test-Path -LiteralPath $localDotnet) {
    $env:DOTNET_ROOT = Split-Path $localDotnet -Parent
    $env:PATH = "$env:DOTNET_ROOT;$env:PATH"
    & $localDotnet @args
} else {
    & dotnet @args
}
exit $LASTEXITCODE
