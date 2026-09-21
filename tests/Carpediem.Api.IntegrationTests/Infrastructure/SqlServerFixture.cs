using Carpediem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;

namespace Carpediem.Api.IntegrationTests.Infrastructure;

public sealed class SqlServerFixture : IAsyncLifetime
{
    public string ConnectionString { get; } = Environment.GetEnvironmentVariable("CARPEDIEM_TEST_SQL_CONNECTION_STRING")
        ?? throw new InvalidOperationException(
            "CARPEDIEM_TEST_SQL_CONNECTION_STRING is required. Run tests/run-sql-integration.ps1.");

    public async Task InitializeAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
        Exception? lastError = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                await using var connection = new SqlConnection(new SqlConnectionStringBuilder(ConnectionString)
                {
                    InitialCatalog = "master",
                    ConnectTimeout = 2,
                }.ConnectionString);
                await connection.OpenAsync();
                return;
            }
            catch (SqlException error)
            {
                lastError = error;
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        throw new TimeoutException("SQL Server test container did not become ready.", lastError);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public static async Task ResetAsync(IServiceProvider services)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<CarpediemDbContext>();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

}

[CollectionDefinition(nameof(SqlServerFixtureGroup))]
public sealed class SqlServerFixtureGroup : ICollectionFixture<SqlServerFixture>;
