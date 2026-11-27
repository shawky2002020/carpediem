$ErrorActionPreference = 'Stop'

Set-Location $PSScriptRoot

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker is required. Install Docker Desktop and start it before running this script.'
}

if (-not (Test-Path '.env')) {
    @'
CARPEDIEM_SQL_PASSWORD=Carpediem#2026DevSql!
DevelopmentSeed__Password=Carpediem#2026SeedAdmin!
'@ | Set-Content -Path '.env' -Encoding utf8
    Write-Host 'Created .env with local development credentials.'
}

docker compose --profile stack up --build -d
if ($LASTEXITCODE -ne 0) {
    throw 'Failed to start the stack. Check Docker and the output above.'
}

Write-Host 'Carpediem is starting at https://localhost:8443'
Write-Host 'The local HTTPS certificate is self-signed. Stop the stack with: docker compose --profile stack down'
