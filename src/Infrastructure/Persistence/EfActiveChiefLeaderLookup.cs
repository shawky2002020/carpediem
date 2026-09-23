using Carpediem.Modules.Events.Application.ChiefLeaderResponses;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Infrastructure.Persistence;

public sealed class EfActiveChiefLeaderLookup(CarpediemDbContext db) : IActiveChiefLeaderLookup
{
    public Task<bool> IsActiveChiefLeaderAsync(Guid eventId, Guid userId,
        CancellationToken cancellationToken) =>
        db.ChiefLeaderAssignments.AsNoTracking().AnyAsync(item =>
            item.EventId == eventId && item.ChiefLeaderUserId == userId &&
            item.Status == ChiefLeaderAssignmentStatus.Active, cancellationToken);
}
