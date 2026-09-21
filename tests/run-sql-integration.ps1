[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
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

    dotnet test tests/Carpediem.Api.IntegrationTests/Carpediem.Api.IntegrationTests.csproj
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Remove-Item Env:\CARPEDIEM_TEST_SQL_CONNECTION_STRING -ErrorAction SilentlyContinue
    docker rm --force $containerName | Out-Null
}
