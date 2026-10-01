using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Modules.Identity.Infrastructure;

public static class DevelopmentIdentitySeeder
{
    private static readonly (string UserName, string Role)[] Users =
    [
        ("admin", CarpediemRoles.Admin),
        ("chief.one", CarpediemRoles.ChiefLeader),
        ("chief.two", CarpediemRoles.ChiefLeader),
        ("leader.one", CarpediemRoles.Leader),
        ("leader.two", CarpediemRoles.Leader),
        ("leader.three", CarpediemRoles.Leader),
        ("leader.four", CarpediemRoles.Leader),
        ("leader.five", CarpediemRoles.Leader),
    ];

    public static async Task SeedDevelopmentIdentityAsync(
        this IServiceProvider services,
        bool isDevelopment,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        if (!isDevelopment)
        {
            return;
        }

        var password = configuration["DevelopmentSeed:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<CarpediemUser>>();

        foreach (var role in CarpediemRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole<Guid>(role));
                EnsureSucceeded(roleResult, $"create development role {role}");
            }
        }

        foreach (var (userName, role) in Users)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var user = await userManager.FindByNameAsync(userName);
            if (user is null)
            {
                user = new CarpediemUser
                {
                    Id = Guid.NewGuid(),
                    UserName = userName,
                    Email = $"{userName}@localhost.invalid",
                    EmailConfirmed = true,
                    IsActive = true,
                };
                EnsureSucceeded(await userManager.CreateAsync(user, password), $"create development user {userName}");
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                EnsureSucceeded(await userManager.AddToRoleAsync(user, role), $"assign {userName} to {role}");
            }
        }

        for (var i = 1; i <= 100; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var userName = $"leader.{i}";
            var user = await userManager.FindByNameAsync(userName);
            if (user is null)
            {
                user = new CarpediemUser
                {
                    Id = Guid.NewGuid(),
                    UserName = userName,
                    Email = $"{userName}@localhost.invalid",
                    EmailConfirmed = true,
                    IsActive = true,
                };
                EnsureSucceeded(await userManager.CreateAsync(user, password), $"create development user {userName}");
            }

            if (!await userManager.IsInRoleAsync(user, CarpediemRoles.Leader))
            {
                EnsureSucceeded(await userManager.AddToRoleAsync(user, CarpediemRoles.Leader), $"assign {userName} to {CarpediemRoles.Leader}");
            }
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(error => error.Code));
        throw new InvalidOperationException($"Could not {operation}: {errors}");
    }
}
