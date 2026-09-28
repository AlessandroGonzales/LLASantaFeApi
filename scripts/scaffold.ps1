# Ejecutar después de aplicar todos los scripts de database, incluido 002_usuario_seguridad.sql.
# Definir ConnectionStrings__DefaultConnection o usar appsettings.Local.json en desarrollo.
# Requiere revisar/guardar los cambios de Entities y del contexto antes de regenerarlos.
param([switch]$Overwrite)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
if (-not $Overwrite) {
    throw 'Este comando regenera los archivos EF. Revisá tus cambios y ejecutá con -Overwrite.'
}
Push-Location $taskRoot
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
try {
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    & "$PSScriptRoot/dotnet.ps1" tool restore
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo restaurar dotnet-ef.' }
    & "$PSScriptRoot/dotnet.ps1" ef dbcontext scaffold 'Name=ConnectionStrings:DefaultConnection' Npgsql.EntityFrameworkCore.PostgreSQL `
        --project Infrastructure --startup-project Presentation `
        --output-dir Persistence/Entities --context-dir Persistence `
        --context LLASantaFeDbContext --namespace Infrastructure.Persistence.Entities `
        --context-namespace Infrastructure.Persistence --no-onconfiguring --force
    if ($LASTEXITCODE -ne 0) { throw 'Falló el scaffolding.' }
} finally {
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    Pop-Location
}
