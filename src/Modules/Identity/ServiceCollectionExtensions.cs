using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Application;
using Carpediem.Modules.Identity.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Carpediem.Modules.Identity;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCarpediemAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireRole(CarpediemRoles.Admin)
                    .AddRequirements(new CurrentAdminRequirement()))
            .AddPolicy(AuthorizationPolicies.RequireChiefLeader, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireRole(CarpediemRoles.ChiefLeader))
            .AddPolicy(AuthorizationPolicies.RequireLeader, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireRole(CarpediemRoles.Leader))
            .AddPolicy(AuthorizationPolicies.RequireChiefLeaderOrAdmin, policy =>
                policy.RequireAuthenticatedUser()
                    .RequireRole(CarpediemRoles.ChiefLeader, CarpediemRoles.Admin));
        services.AddScoped<IAuthorizationHandler, CurrentAdminAuthorizationHandler>();
        services.AddScoped<IChiefLeaderCandidateDirectory, EfChiefLeaderCandidateDirectory>();
        services.AddScoped<IChiefLeaderResponseDirectory, EfChiefLeaderResponseDirectory>();

        return services;
    }
}
