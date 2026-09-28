$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
Push-Location $taskRoot
try {
    & "$PSScriptRoot/dotnet.ps1" restore LLASantaFeApi.sln --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Falló la restauración de NuGet.' }
    & "$PSScriptRoot/dotnet.ps1" build Presentation/Presentation.csproj --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación.' }
    Write-Host 'Swagger: http://localhost:5275/swagger (Ctrl+C para detener)'
    & "$PSScriptRoot/dotnet.ps1" run --project Presentation --no-build --launch-profile http
} finally {
    Pop-Location
}
