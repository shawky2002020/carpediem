[CmdletBinding()]
param(
    [switch]$ContainerTestHost
)

$ErrorActionPreference = 'Stop'

if ($ContainerTestHost) {
    $networkName = "carpediem-tests-$([Guid]::NewGuid().ToString('N'))"
    $sqlContainerName = "carpediem-sql-$([Guid]::NewGuid().ToString('N'))"
    $testContainerName = "carpediem-testhost-$([Guid]::NewGuid().ToString('N'))"
    $containerPassword = "Cd!$([Guid]::NewGuid().ToString('N'))aA1"
    $sourcePath = (Get-Location).Path
    $nugetCache = Join-Path $env:USERPROFILE '.nuget\packages'
    try {
        docker network create $networkName | Out-Null
        docker run --detach --name $sqlContainerName --network $networkName --network-alias sql `
            --env ACCEPT_EULA=Y --env "MSSQL_SA_PASSWORD=$containerPassword" `
            mcr.microsoft.com/mssql/server:2025-latest | Out-Null

        $databaseReady = $false
        foreach ($attempt in 1..30) {
            $databaseLogs = docker logs $sqlContainerName 2>&1
            if ($databaseLogs -match 'SQL Server is now ready for client connections') {
                $databaseReady = $true
                break
            }
            Start-Sleep -Seconds 1
        }
        if (-not $databaseReady) { throw 'SQL Server did not become ready before the retry limit.' }

        $containerConnection =
            "Server=sql,1433;Initial Catalog=CarpediemIntegration;User ID=sa;Password=$containerPassword;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5"
        docker run --name $testContainerName --network $networkName --dns 8.8.8.8 `
            --env "CARPEDIEM_TEST_SQL_CONNECTION_STRING=$containerConnection" `
            --env NUGET_PACKAGES=/nuget `
            --volume "${sourcePath}:/source:ro" `
            --volume "${nugetCache}:/nuget:ro" `
            mcr.microsoft.com/dotnet/sdk:10.0 bash -lc `
            "mkdir -p /workspace && tar -C /source --exclude=.git --exclude=logo.pdf --exclude='*/node_modules' --exclude='*/bin' --exclude='*/obj' --exclude='*/dist' --exclude='*/test-results' --exclude='*/playwright-report' -cf - . | tar -C /workspace -xf - && cd /workspace && dotnet test tests/Carpediem.Api.IntegrationTests/Carpediem.Api.IntegrationTests.csproj --configuration Release --logger 'console;verbosity=minimal'"
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        return
    }
    finally {
        docker rm --force $testContainerName 2>$null | Out-Null
        docker rm --force $sqlContainerName 2>$null | Out-Null
        docker network rm $networkName 2>$null | Out-Null
    }
}

$containerName = "carpediem-tests-$([Guid]::NewGuid().ToString('N'))"
$password = "Cd!$([Guid]::NewGuid().ToString('N'))aA1"
$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()

try {
    $containerId = docker run --detach --name $containerName `
        --env ACCEPT_EULA=Y `
        --env "MSSQL_SA_PASSWORD=$password" `
        --publish "127.0.0.1:${port}:1433" `
        mcr.microsoft.com/mssql/server:2025-latest
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($containerId)) {
        throw 'The SQL Server 2025 test container could not be started.'
    }

    $env:CARPEDIEM_TEST_SQL_CONNECTION_STRING =
        "Server=127.0.0.1,$port;Initial Catalog=CarpediemIntegration;User ID=sa;Password=$password;Encrypt=True;TrustServerCertificate=True;Connect Timeout=5"

    # Release output is accepted by Windows Application Control on the supported dev host;
    # freshly emitted Debug assemblies may be blocked before xUnit can load them.
    dotnet test tests/Carpediem.Api.IntegrationTests/Carpediem.Api.IntegrationTests.csproj --configuration Release
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Remove-Item Env:\CARPEDIEM_TEST_SQL_CONNECTION_STRING -ErrorAction SilentlyContinue
    docker rm --force $containerName | Out-Null
}
