using Carpediem.Modules.Identity.Application;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Identity.Domain;
using Microsoft.AspNetCore.Identity;

namespace Carpediem.Modules.Identity.Infrastructure;

public sealed class EfChiefLeaderCandidateDirectory(UserManager<CarpediemUser> users)
    : IChiefLeaderCandidateDirectory
{
    public async Task<IReadOnlyList<ChiefLeaderCandidate>> ListEligibleAsync(CancellationToken cancellationToken)
    {
        var members = await users.GetUsersInRoleAsync(CarpediemRoles.ChiefLeader);
        cancellationToken.ThrowIfCancellationRequested();
        return members
            .Where(user => user.IsActive && !string.IsNullOrWhiteSpace(user.UserName))
            .OrderBy(user => user.UserName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(user => user.Id)
            .Select(user => new ChiefLeaderCandidate(user.Id, user.UserName!))
            .ToArray();
    }

    public async Task<ChiefLeaderCandidate?> FindEligibleAsync(Guid userId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is not { IsActive: true } || string.IsNullOrWhiteSpace(user.UserName) ||
            !await users.IsInRoleAsync(user, CarpediemRoles.ChiefLeader))
        {
            return null;
        }
        return new ChiefLeaderCandidate(user.Id, user.UserName);
    }
}
