[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

$runId = [Guid]::NewGuid().ToString('N')
$networkName = "carpediem-performance-$runId"
$sqlContainerName = "carpediem-performance-sql-$runId"
$apiContainerName = "carpediem-performance-api-$runId"
$databasePassword = "Cd!$([Guid]::NewGuid().ToString('N'))aA1"
$seedPassword = "Cd!$([Guid]::NewGuid().ToString('N'))aA1"
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$apiPort = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
$sourcePath = (Get-Location).Path
$nugetCache = Join-Path $env:USERPROFILE '.nuget\packages'
$completed = $false

try {
    docker network create $networkName | Out-Null
    $sqlContainerId = docker run --detach --name $sqlContainerName `
        --network $networkName --network-alias sql `
        --env ACCEPT_EULA=Y `
        --env "MSSQL_SA_PASSWORD=$databasePassword" `
        mcr.microsoft.com/mssql/server:2025-latest
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sqlContainerId)) {
        throw 'The SQL Server performance container could not be started.'
    }

    $databaseReady = $false
    foreach ($attempt in 1..30) {
        if ((docker logs $sqlContainerName 2>&1) -match
            'SQL Server is now ready for client connections') {
            $databaseReady = $true
            break
        }
        Start-Sleep -Seconds 1
    }
    if (-not $databaseReady) {
        throw 'SQL Server did not become ready before the retry limit.'
    }

    $connectionString =
        "Server=sql,1433;Initial Catalog=CarpediemPerformance;User ID=sa;Password=$databasePassword;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5"
    $containerCommand = @'
mkdir -p /workspace &&
tar -C /source --exclude=.git --exclude=logo.pdf --exclude='*.docx' --exclude='*/node_modules' --exclude='*/bin' --exclude='*/obj' --exclude='*/dist' --exclude='*/test-results' --exclude='*/playwright-report' -cf - . | tar -C /workspace -xf - &&
cd /workspace &&
dotnet tool restore &&
dotnet build Carpediem.slnx --configuration Release &&
dotnet ef database update --project src/Infrastructure/Carpediem.Infrastructure.csproj --startup-project src/Carpediem.Web/Carpediem.Web.csproj --configuration Release --no-build &&
dotnet dev-certs https --export-path /tmp/carpediem-performance.pfx --password Carpediem-Performance-Cert &&
dotnet src/Carpediem.Web/bin/Release/net10.0/Carpediem.Web.dll --urls=https://0.0.0.0:8443
'@
    $apiContainerId = docker run --detach --name $apiContainerName `
        --network $networkName --dns 8.8.8.8 `
        --env "ConnectionStrings__Carpediem=$connectionString" `
        --env "DevelopmentSeed__Password=$seedPassword" `
        --env ASPNETCORE_ENVIRONMENT=Development `
        --env ASPNETCORE_Kestrel__Certificates__Default__Path=/tmp/carpediem-performance.pfx `
        --env ASPNETCORE_Kestrel__Certificates__Default__Password=Carpediem-Performance-Cert `
        --env NUGET_PACKAGES=/nuget `
        --publish "127.0.0.1:${apiPort}:8443" `
        --volume "${sourcePath}:/source:ro" `
        --volume "${nugetCache}:/nuget:ro" `
        mcr.microsoft.com/dotnet/sdk:10.0 bash -lc $containerCommand
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($apiContainerId)) {
        throw 'The API performance container could not be started.'
    }

    $healthy = $false
    foreach ($attempt in 1..180) {
        $httpCode = & curl.exe -k -s -o /dev/null -w "%{http_code}" "https://127.0.0.1:$apiPort/healthz"
        if ($httpCode -eq '200') {
            $healthy = $true
            break
        }
        if ((docker inspect --format '{{.State.Running}}' $apiContainerName 2>$null) -eq 'false') {
            throw "The API exited before becoming healthy. $(docker logs $apiContainerName 2>&1)"
        }
        Start-Sleep -Seconds 1
    }
    if (-not $healthy) {
        throw "The API did not become healthy before the retry limit. $(docker logs $apiContainerName 2>&1)"
    }

    $openApiRaw = & curl.exe -k -s "https://127.0.0.1:$apiPort/openapi/v1.json"
    $openApi = $openApiRaw | ConvertFrom-Json
    $path = $openApi.paths.PSObject.Properties['/api/events/drafts/{id}/publish'].Value
    if (-not $path.post) {
        throw 'Generated OpenAPI is missing the publish operation.'
    }
    $operationJson = $path.post | ConvertTo-Json -Depth 100
    foreach ($requiredText in @('If-Match', 'X-XSRF-TOKEN', 'Idempotent-Replay')) {
        if ($operationJson -notmatch [regex]::Escape($requiredText)) {
            throw "Generated OpenAPI is missing $requiredText from publish."
        }
    }
    $documentJson = $openApi | ConvertTo-Json -Depth 100
    foreach ($requiredText in @('activeChiefLeader', 'publishedAt')) {
        if ($documentJson -notmatch [regex]::Escape($requiredText)) {
            throw "Generated OpenAPI is missing $requiredText from schemas."
        }
    }

    $env:BASE_URL = "https://127.0.0.1:$apiPort"
    $env:CARPEDIEM_ADMIN_USERNAME = 'admin'
    $env:CARPEDIEM_ADMIN_PASSWORD = $seedPassword
    New-Item -ItemType Directory -Force 'artifacts/performance' | Out-Null
    k6 run `
        --summary-export 'artifacts/performance/005-event-publication.json' `
        'tests/performance/event-publication.k6.js'
    if ($LASTEXITCODE -ne 0) {
        throw 'The Event publication k6 workload failed.'
    }
    $completed = $true
}
catch {
    Write-Host "FAILURE OCCURRED: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor Yellow
    throw
}
finally {
    Remove-Item Env:\BASE_URL -ErrorAction SilentlyContinue
    Remove-Item Env:\CARPEDIEM_ADMIN_USERNAME -ErrorAction SilentlyContinue
    Remove-Item Env:\CARPEDIEM_ADMIN_PASSWORD -ErrorAction SilentlyContinue
    if (-not $completed) {
        docker logs $apiContainerName 2>$null
    }
    docker rm --force $apiContainerName 2>$null | Out-Null
    docker rm --force $sqlContainerName 2>$null | Out-Null
    docker network rm $networkName 2>$null | Out-Null
}
