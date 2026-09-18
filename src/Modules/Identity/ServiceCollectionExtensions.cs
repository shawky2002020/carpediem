using Carpediem.Modules.Identity.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Modules.Identity;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCarpediemAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
                policy.RequireAuthenticatedUser().RequireRole(CarpediemRoles.Admin));

        return services;
    }
}
