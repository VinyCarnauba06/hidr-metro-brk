# scripts/deploy/install-services.ps1
#
# Registra HidrometroAPI e HidrometroWeb como serviços Windows via NSSM.
# Idempotente: pode ser rodado de novo (atualiza publish + reconfigura serviços existentes).
# Requer: PowerShell como Administrador, .NET 8 SDK (para publish), NSSM no PATH.
#
# Uso:
#   .\install-services.ps1 -EnvFile C:\hidrometro\.env.prod
#   .\install-services.ps1 -EnvFile C:\hidrometro\.env.prod -PostgresMode native
#
# O EnvFile é um .env padrão (KEY=VALUE, uma por linha, # para comentário) com os
# valores reais de produção. Nunca commitar esse arquivo — fica fora do repo.
# Ver .env.example na raiz do repo para a lista de chaves esperadas.

[CmdletBinding()]
param(
    [string]$RepoPath = (Resolve-Path "$PSScriptRoot\..\..").Path,
    [string]$DeployRoot = "C:\hidrometro",
    [Parameter(Mandatory = $true)]
    [string]$EnvFile,
    [ValidateSet("docker", "native")]
    [string]$PostgresMode = "docker",
    [switch]$SkipPublish,
    [switch]$SkipHealthCheck
)

$ErrorActionPreference = "Stop"

function Assert-Admin {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        throw "Rode este script como Administrador (PowerShell -> Executar como administrador)."
    }
}

function Assert-Command($name, $installHint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) {
        throw "'$name' não encontrado no PATH. $installHint"
    }
}

function Read-EnvFile($path) {
    if (-not (Test-Path $path)) {
        throw "EnvFile não encontrado: $path"
    }
    $map = @{}
    Get-Content $path | ForEach-Object {
        $line = $_.Trim()
        if ($line -eq "" -or $line.StartsWith("#")) { return }
        $idx = $line.IndexOf("=")
        if ($idx -lt 1) { return }
        $key = $line.Substring(0, $idx).Trim()
        $val = $line.Substring($idx + 1).Trim()
        $map[$key] = $val
    }
    return $map
}

function Assert-Required($env, [string[]]$keys, $context) {
    $missing = $keys | Where-Object { -not $env.ContainsKey($_) -or [string]::IsNullOrWhiteSpace($env[$_]) }
    if ($missing.Count -gt 0) {
        throw "Faltando no EnvFile para $context : $($missing -join ', ')"
    }
}

function Resolve-PostgresConflict($mode) {
    Write-Host "Resolvendo conflito de porta 5432 (modo: $mode)..." -ForegroundColor Cyan
    $nativeSvc = Get-Service -Name "postgresql*" -ErrorAction SilentlyContinue | Select-Object -First 1

    if ($mode -eq "docker") {
        if ($nativeSvc -and $nativeSvc.Status -eq "Running") {
            Write-Host "  Parando serviço Postgres nativo ($($nativeSvc.Name)) e desabilitando auto-start..."
            Stop-Service $nativeSvc.Name -Force
            Set-Service $nativeSvc.Name -StartupType Manual
        }
        $running = docker ps --filter "name=hidrometro_db" --filter "status=running" --format "{{.Names}}" 2>$null
        if (-not $running) {
            Write-Host "  Subindo container hidrometro_db via docker compose..."
            Push-Location $RepoPath
            docker compose up -d db
            Pop-Location
        } else {
            Write-Host "  Container hidrometro_db já rodando."
        }
    } else {
        $dockerRunning = docker ps --filter "name=hidrometro_db" --filter "status=running" --format "{{.Names}}" 2>$null
        if ($dockerRunning) {
            Write-Host "  Parando container Docker hidrometro_db..."
            docker stop hidrometro_db | Out-Null
        }
        if ($nativeSvc) {
            if ($nativeSvc.Status -ne "Running") {
                Write-Host "  Iniciando serviço Postgres nativo ($($nativeSvc.Name))..."
                Set-Service $nativeSvc.Name -StartupType Automatic
                Start-Service $nativeSvc.Name
            } else {
                Write-Host "  Postgres nativo já rodando."
            }
        } else {
            Write-Warning "  Nenhum serviço postgresql* encontrado. Instale o PostgreSQL 16 nativo antes de usar -PostgresMode native."
        }
    }
}

function Publish-Project($csprojDir, $outDir) {
    Write-Host "Publicando $csprojDir -> $outDir ..." -ForegroundColor Cyan
    dotnet publish $csprojDir -c Release -o $outDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou para $csprojDir" }
}

function Install-NssmService($name, $exePath, $args, $appDir, $envPairs) {
    $exists = (nssm status $name 2>$null)
    if ($LASTEXITCODE -eq 0 -and $exists) {
        Write-Host "  Serviço $name já existe — parando para reconfigurar..." -ForegroundColor Yellow
        nssm stop $name | Out-Null
        nssm remove $name confirm | Out-Null
    }

    Write-Host "  Instalando serviço $name..." -ForegroundColor Cyan
    nssm install $name $exePath $args
    nssm set $name AppDirectory $appDir
    nssm set $name AppEnvironmentExtra $envPairs
    nssm set $name Start SERVICE_AUTO_START
    nssm set $name AppStdout "$appDir\..\logs\$name.out.log"
    nssm set $name AppStderr "$appDir\..\logs\$name.err.log"
    nssm set $name AppRotateFiles 1
    nssm set $name AppRotateBytes 10485760

    nssm start $name
    Start-Sleep -Seconds 2
    $status = nssm status $name
    Write-Host "  $name -> $status"
}

function Test-Health($url, $label, $retries = 10, $delaySec = 3) {
    Write-Host "Checando saúde: $label ($url)..." -ForegroundColor Cyan
    for ($i = 1; $i -le $retries; $i++) {
        try {
            $resp = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5
            if ($resp.StatusCode -eq 200) {
                Write-Host "  OK ($($resp.StatusCode))" -ForegroundColor Green
                return $true
            }
        } catch {
            Write-Host "  tentativa $i/$retries falhou, aguardando ${delaySec}s..."
        }
        Start-Sleep -Seconds $delaySec
    }
    Write-Warning "  $label não respondeu 200 após $retries tentativas."
    return $false
}

# ---- main ----

Assert-Admin
Assert-Command "dotnet" "Instale: winget install Microsoft.DotNet.AspNetCore.8 && winget install Microsoft.DotNet.SDK.8"
Assert-Command "nssm" "Instale: winget install NSSM.NSSM"

$env = Read-EnvFile $EnvFile
Assert-Required $env @("DATABASE_URL", "JWT_SECRET", "ALLOWED_ORIGINS") "API"

$usingGcs = -not [string]::IsNullOrWhiteSpace($env["GCS_BUCKET_NAME"])
if ($usingGcs) {
    Assert-Required $env @("GOOGLE_APPLICATION_CREDENTIALS") "GCS storage (GCS_BUCKET_NAME setado)"
    if (-not (Test-Path $env["GOOGLE_APPLICATION_CREDENTIALS"])) {
        throw "GOOGLE_APPLICATION_CREDENTIALS aponta para arquivo inexistente: $($env['GOOGLE_APPLICATION_CREDENTIALS'])"
    }
}

$apiDir = Join-Path $DeployRoot "api"
$webDir = Join-Path $DeployRoot "web"
$logsDir = Join-Path $DeployRoot "logs"
$storageDir = Join-Path $DeployRoot "storage\fotos"
New-Item -ItemType Directory -Force -Path $logsDir | Out-Null
if (-not $usingGcs) { New-Item -ItemType Directory -Force -Path $storageDir | Out-Null }

Resolve-PostgresConflict $PostgresMode

if (-not $SkipPublish) {
    Publish-Project (Join-Path $RepoPath "backend\src\HidrometroApp.Api") $apiDir
    Publish-Project (Join-Path $RepoPath "web") $webDir
} else {
    Write-Host "Pulando dotnet publish (-SkipPublish)." -ForegroundColor Yellow
}

$jwtHours = if ([string]::IsNullOrWhiteSpace($env['JWT_EXPIRATION_HOURS'])) { '8' } else { $env['JWT_EXPIRATION_HOURS'] }
$apiUrlForWeb = if ([string]::IsNullOrWhiteSpace($env['API_URL'])) { 'http://localhost:5000' } else { $env['API_URL'] }

$apiEnvPairs = @(
    "ASPNETCORE_URLS=http://+:5000",
    "ASPNETCORE_ENVIRONMENT=Production",
    "DATABASE_URL=$($env['DATABASE_URL'])",
    "JWT_SECRET=$($env['JWT_SECRET'])",
    "JWT_EXPIRATION_HOURS=$jwtHours",
    "LOG_PATH=$logsDir\app.log",
    "ALLOWED_ORIGINS=$($env['ALLOWED_ORIGINS'])",
    "GEMINI_API_KEY=$($env['GEMINI_API_KEY'])",
    "GCS_BUCKET_NAME=$($env['GCS_BUCKET_NAME'])",
    "GOOGLE_APPLICATION_CREDENTIALS=$($env['GOOGLE_APPLICATION_CREDENTIALS'])",
    "GOOGLE_CLIENT_ID=$($env['GOOGLE_CLIENT_ID'])",
    "STORAGE_PATH=$storageDir"
) -join "`r`n"

$webEnvPairs = @(
    "ASPNETCORE_URLS=http://+:5001",
    "ASPNETCORE_ENVIRONMENT=Production",
    "API_URL=$apiUrlForWeb"
) -join "`r`n"

$dotnetExe = (Get-Command dotnet).Source

Install-NssmService "HidrometroAPI" $dotnetExe "`"$apiDir\HidrometroApp.Api.dll`"" $apiDir $apiEnvPairs
Install-NssmService "HidrometroWeb" $dotnetExe "`"$webDir\HidrometroApp.Web.dll`"" $webDir $webEnvPairs

if (-not $SkipHealthCheck) {
    Start-Sleep -Seconds 3
    Test-Health "http://localhost:5000/api/health" "API" | Out-Null
    Test-Health "http://localhost:5001" "Web" | Out-Null
}

Write-Host ""
Write-Host "Deploy concluído." -ForegroundColor Green
Write-Host "  API : http://localhost:5000  (serviço HidrometroAPI, storage: $(if ($usingGcs) { "GCS $($env['GCS_BUCKET_NAME'])" } else { $storageDir }))"
Write-Host "  Web : http://localhost:5001  (serviço HidrometroWeb)"
Write-Host "  Logs: $logsDir"
Write-Host ""
Write-Host "Rodar de novo com -SkipPublish para só reconfigurar os serviços sem republicar."
