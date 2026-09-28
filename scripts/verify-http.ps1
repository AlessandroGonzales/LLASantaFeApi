# Prueba de compatibilidad HTTP contra una base DE PRUEBA, sin datos reales.
# Requiere esquema + 001 + 002 y rol Usuario. La cuenta sintética queda en esa base.
param(
    [Parameter(Mandatory)][string]$TestConnectionString,
    [int]$Port = 5189
)
$ErrorActionPreference = 'Stop'
if ($TestConnectionString -notmatch '(?i)Database=net10_[a-z_]+(?:;|$)') {
    throw 'Solo se admiten bases de prueba con nombre net10_*.'
}
$taskRoot = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $taskRoot '.artifacts'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$previousConnection = $env:ConnectionStrings__DefaultConnection
$previousKey = $env:JwtSettings__Key
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
$api = $null
try {
    $env:ConnectionStrings__DefaultConnection = $TestConnectionString
    $env:JwtSettings__Key = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    $dotnet = Join-Path $taskRoot '.tools/dotnet/dotnet.exe'
    if (-not (Test-Path $dotnet)) { $dotnet = (Get-Command dotnet).Source }
    $api = Start-Process -FilePath $dotnet -ArgumentList @('bin/Release/net10.0/Presentation.dll', '--urls', "http://127.0.0.1:$Port") `
        -WorkingDirectory (Join-Path $taskRoot 'Presentation') -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $artifacts 'http-smoke.stdout.log') `
        -RedirectStandardError (Join-Path $artifacts 'http-smoke.stderr.log')
    $base = "http://127.0.0.1:$Port"
    $swagger = $null
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($api.HasExited) { throw 'La API terminó antes de iniciar. Revisar .artifacts/http-smoke.*.log.' }
        try { $swagger = Invoke-RestMethod "$base/swagger/v1/swagger.json"; break } catch { Start-Sleep -Milliseconds 300 }
    }
    if (-not $swagger) { throw 'Swagger no respondió.' }
    if ($swagger.components.securitySchemes.Bearer.scheme -ne 'bearer') { throw 'Esquema JWT inválido en Swagger.' }
    $anonymous = Invoke-WebRequest "$base/api/Usuario/me" -SkipHttpErrorCheck
    if ($anonymous.StatusCode -ne 401) { throw 'El perfil anónimo debe responder 401.' }
    $email = "Net10.$([Guid]::NewGuid().ToString('N'))@example.invalid"
    $password = 'Synthetic-Test-Only!9362'
    $registration = @{
        nombre = 'Prueba'; apellido = 'Net10'; email = $email; password = $password
        fechaNacimiento = '1990-01-01'
    } | ConvertTo-Json
    Invoke-RestMethod "$base/api/Usuario" -Method Post -ContentType 'application/json' -Body $registration | Out-Null
    # Se consulta con mayúsculas/espacios distintos: prueba el nuevo índice normalizado.
    $login = @{ email = " $($email.ToUpperInvariant()) "; password = $password } | ConvertTo-Json
    $session = Invoke-RestMethod "$base/api/Usuario/login" -Method Post -ContentType 'application/json' -Body $login
    if (-not $session.token) { throw 'No se emitió JWT.' }
    $profile = Invoke-RestMethod "$base/api/Usuario/me" -Headers @{Authorization = "Bearer $($session.token)"}
    if ($profile.email -ne $email) { throw 'El perfil no conserva el email original.' }
    Write-Output 'OK: Swagger/OpenAPI, 401 anónimo, registro EF10, login normalizado, JWT y perfil autenticado.'
} finally {
    if ($api -and -not $api.HasExited) { Stop-Process -Id $api.Id; $api.WaitForExit() }
    $env:ConnectionStrings__DefaultConnection = $previousConnection
    $env:JwtSettings__Key = $previousKey
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
}
