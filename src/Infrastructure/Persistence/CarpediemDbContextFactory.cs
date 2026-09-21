using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Carpediem.Infrastructure.Persistence;

public sealed class CarpediemDbContextFactory : IDesignTimeDbContextFactory<CarpediemDbContext>
{
    public CarpediemDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Carpediem")
            ?? "Server=localhost,14333;Database=Carpediem;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<CarpediemDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(CarpediemDbContext).Assembly.FullName))
            .Options;
        return new CarpediemDbContext(options);
    }
}
