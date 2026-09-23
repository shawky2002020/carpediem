using Carpediem.Modules.Identity.Application;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;

namespace Carpediem.Modules.Identity.Infrastructure;

public sealed class EfChiefLeaderResponseDirectory(UserManager<CarpediemUser> users)
    : IChiefLeaderResponseDirectory
{
    public async Task<bool> IsActiveChiefLeaderAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId.ToString());
        return user is { IsActive: true } &&
            await users.IsInRoleAsync(user, CarpediemRoles.ChiefLeader);
    }

    public async Task<IReadOnlyList<Guid>> ListActiveAdminIdsAsync(CancellationToken cancellationToken)
    {
        var members = await users.GetUsersInRoleAsync(CarpediemRoles.Admin);
        cancellationToken.ThrowIfCancellationRequested();
        return members.Where(user => user.IsActive).Select(user => user.Id).Distinct().ToArray();
    }
}
