param([ValidateSet('Staged','Worktree','History')][string]$Mode='Staged')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$scanner=Join-Path $root '.tools/gitleaks/gitleaks.exe'
if(-not(Test-Path $scanner)){throw 'Instalá Gitleaks con scripts/install-gitleaks.ps1 antes de continuar.'}
Push-Location $root
try {
    $audit=Join-Path $root ('.artifacts/secret-audit/'+[Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force $audit|Out-Null
    $report=Join-Path $audit 'report.json'
    $log=Join-Path $audit 'scan.log'
    if($Mode -eq 'History') {
        $ErrorActionPreference='Continue' # Windows PowerShell 5 trata stderr de herramientas nativas como errores.
        & $scanner git . --log-opts='--all' --config .gitleaks.toml --redact=100 --report-format json --report-path $report --no-banner > $log 2>&1
        $ErrorActionPreference='Stop'
    } else {
        $snapshot=Join-Path $audit 'snapshot'
        New-Item -ItemType Directory -Force $snapshot|Out-Null
        # Analiza el árbol completo del índice, no solo el diff. Worktree incluye nuevos archivos no ignorados.
        $paths=if($Mode -eq 'Staged'){@(git -c core.quotepath=false ls-files)}else{@(git -c core.quotepath=false ls-files --cached --others --exclude-standard)}
        if($LASTEXITCODE -ne 0){throw 'No se pudo enumerar Git.'}
        $forbidden='(?i)(^|/)(\.env(?:\..+)?|appsettings\.(?:.*\.)?local\.json|secrets\.json|credentials\.json|token\.json|client_secret[^/]*\.json)$|\.(pem|key|pfx|p12|jks|dump|backup|bak|publishsettings|pubxml)$|(^|/)(bin|obj|\.artifacts|\.tools|\.vs|backups|uploads)/'
        foreach($path in $paths | Select-Object -Unique) {
            if($path -match $forbidden -and $path -ne '.env.example'){throw "Archivo privado o generado dentro de Git: $path"}
            if($Mode -eq 'Staged') {
                $lines=@(git show (':'+$path) 2>$null)
                if($LASTEXITCODE -ne 0){throw "No se pudo leer el índice: $path"}
                $content=$lines -join "`n"
            } else {
                if(-not(Test-Path -LiteralPath $path -PathType Leaf)){continue}
                $content=Get-Content -LiteralPath $path -Raw
            }
            if($path -match '(^|/)appsettings[^/]*\.json$') {
                $settings=$content|ConvertFrom-Json
                foreach($value in @($settings.ConnectionStrings.DefaultConnection,$settings.JwtSettings.Key,$settings.Gmail.ClientSecret,$settings.Gmail.RefreshToken)) {
                    if(-not [string]::IsNullOrWhiteSpace([string]$value)){throw "Configuración sensible no vacía en $path"}
                }
            }
            $dest=Join-Path $snapshot $path
            New-Item -ItemType Directory -Force (Split-Path $dest)|Out-Null
            [IO.File]::WriteAllText($dest,$content)
        }
        $ErrorActionPreference='Continue'
        & $scanner dir $snapshot --config .gitleaks.toml --redact=100 --report-format json --report-path $report --no-banner > $log 2>&1
        $ErrorActionPreference='Stop'
    }
    $scanExit=$LASTEXITCODE
    if($scanExit -eq 1) {
        foreach($finding in (Get-Content $report -Raw|ConvertFrom-Json)) {
            Write-Host ($finding.RuleID+' '+$finding.File+':'+$finding.StartLine+' Commit='+$finding.Commit)
        }
        throw 'Se detectaron posibles secretos. Operación bloqueada. No se muestran valores.'
    }
    if($scanExit -ne 0){throw "El scanner falló; no se puede confirmar la revisión. Código=$scanExit"}
    Write-Host "OK: revisión $Mode sin hallazgos de Gitleaks ni configuraciones privadas versionables."
} finally {Pop-Location}
