[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$containerName = "carpediem-performance-$([Guid]::NewGuid().ToString('N'))"
$databasePassword = "Cd!$([Guid]::NewGuid().ToString('N'))aA1"
$seedPassword = "Cd!$([Guid]::NewGuid().ToString('N'))aA1"
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$databasePort = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$apiPort = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()
$apiProcess = $null
$apiLog = Join-Path ([System.IO.Path]::GetTempPath()) "$containerName-api.log"
$apiErrorLog = Join-Path ([System.IO.Path]::GetTempPath()) "$containerName-api-error.log"
$completed = $false

try {
    $containerId = docker run --detach --name $containerName `
        --env ACCEPT_EULA=Y `
        --env "MSSQL_SA_PASSWORD=$databasePassword" `
        --publish "127.0.0.1:${databasePort}:1433" `
        mcr.microsoft.com/mssql/server:2025-latest
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($containerId)) {
        throw 'The SQL Server performance container could not be started.'
    }

    $env:ConnectionStrings__Carpediem =
        "Server=127.0.0.1,$databasePort;Initial Catalog=CarpediemPerformance;User ID=sa;Password=$databasePassword;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5"
    $env:DevelopmentSeed__Password = $seedPassword
    $env:ASPNETCORE_ENVIRONMENT = 'Development'

    $databaseReady = $false
    foreach ($attempt in 1..30) {
        $databaseLogs = docker logs $containerName 2>&1
        if ($databaseLogs -match 'SQL Server is now ready for client connections') {
            $databaseReady = $true
            break
        }
        Start-Sleep -Seconds 1
    }
    if (-not $databaseReady) {
        throw 'SQL Server did not become ready before the retry limit.'
    }

    $migrationApplied = $false
    foreach ($attempt in 1..12) {
        dotnet ef database update `
            --project src/Infrastructure/Carpediem.Infrastructure.csproj `
            --startup-project src/Carpediem.Web/Carpediem.Web.csproj `
            --configuration Release --no-build
        if ($LASTEXITCODE -eq 0) {
            $migrationApplied = $true
            break
        }
        Start-Sleep -Seconds 2
    }
    if (-not $migrationApplied) {
        throw 'Database migration did not succeed before the retry limit.'
    }

    $apiProcess = Start-Process dotnet `
        -ArgumentList @('bin/Release/net10.0/Carpediem.Web.dll', "--urls=https://127.0.0.1:$apiPort") `
        -WorkingDirectory 'src/Carpediem.Web' `
        -WindowStyle Hidden `
        -RedirectStandardOutput $apiLog `
        -RedirectStandardError $apiErrorLog `
        -PassThru

    $healthy = $false
    foreach ($attempt in 1..30) {
        try {
            $health = Invoke-WebRequest -UseBasicParsing -SkipCertificateCheck "https://127.0.0.1:$apiPort/healthz"
            if ($health.StatusCode -eq 200) {
                $healthy = $true
                break
            }
        }
        catch {
            if ($apiProcess.HasExited) {
                throw "The API exited before becoming healthy. $((Get-Content -Raw $apiLog -ErrorAction SilentlyContinue)) $((Get-Content -Raw $apiErrorLog -ErrorAction SilentlyContinue))"
            }
        }
        Start-Sleep -Seconds 1
    }
    if (-not $healthy) {
        throw 'The API did not become healthy before the retry limit.'
    }

    $env:BASE_URL = "https://127.0.0.1:$apiPort"
    $env:CARPEDIEM_ADMIN_USERNAME = 'admin'
    $env:CARPEDIEM_ADMIN_PASSWORD = $seedPassword
    New-Item -ItemType Directory -Force 'artifacts/performance' | Out-Null
    k6 run `
        --summary-export 'artifacts/performance/002-chief-leader-appointment.json' `
        'tests/performance/chief-leader-appointment.k6.js'
    if ($LASTEXITCODE -ne 0) {
        throw 'The Chief Leader appointment k6 workload failed.'
    }
    $completed = $true
}
finally {
    if ($apiProcess -and -not $apiProcess.HasExited) {
        Stop-Process -Id $apiProcess.Id -Force
        $apiProcess.WaitForExit()
    }
    docker rm --force $containerName | Out-Null
    Remove-Item Env:\ConnectionStrings__Carpediem -ErrorAction SilentlyContinue
    Remove-Item Env:\DevelopmentSeed__Password -ErrorAction SilentlyContinue
    Remove-Item Env:\ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue
    Remove-Item Env:\BASE_URL -ErrorAction SilentlyContinue
    Remove-Item Env:\CARPEDIEM_ADMIN_USERNAME -ErrorAction SilentlyContinue
    Remove-Item Env:\CARPEDIEM_ADMIN_PASSWORD -ErrorAction SilentlyContinue
    if (-not $completed) {
        Get-Content $apiLog -ErrorAction SilentlyContinue
        Get-Content $apiErrorLog -ErrorAction SilentlyContinue
    }
    Remove-Item -LiteralPath $apiLog -ErrorAction SilentlyContinue
    Remove-Item -LiteralPath $apiErrorLog -ErrorAction SilentlyContinue
}
