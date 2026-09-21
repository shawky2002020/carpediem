using System.Security.Claims;
using Carpediem.Modules.Identity.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

namespace Carpediem.Modules.Identity.Authorization;

public sealed class CurrentAdminRequirement : IAuthorizationRequirement;

public sealed class CurrentAdminAuthorizationHandler(UserManager<CarpediemUser> users)
    : AuthorizationHandler<CurrentAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CurrentAdminRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return;
        }

        var user = await users.FindByIdAsync(userId.ToString());
        if (user is { IsActive: true } && await users.IsInRoleAsync(user, CarpediemRoles.Admin))
        {
            context.Succeed(requirement);
        }
    }
}
