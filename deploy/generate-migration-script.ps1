[CmdletBinding()]
param(
    [string]$OutputPath = "deploy/migrations/v1-idempotent.sql"
)

$ErrorActionPreference = 'Stop'

dotnet tool restore
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$fullOutputPath = Join-Path (Get-Location).Path $OutputPath
$outputDir = Split-Path -Parent $fullOutputPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
}

dotnet ef migrations script `
    --idempotent `
    --project src/Infrastructure/Carpediem.Infrastructure.csproj `
    --startup-project src/Carpediem.Web/Carpediem.Web.csproj `
    --context CarpediemDbContext `
    --configuration Release `
    --output $fullOutputPath

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$content = [System.IO.File]::ReadAllText($fullOutputPath).TrimEnd() + [Environment]::NewLine
[System.IO.File]::WriteAllText($fullOutputPath, $content)

Write-Host "Generated idempotent SQL migration script at $fullOutputPath"
