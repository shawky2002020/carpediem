using System.Security.Claims;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Web.Endpoints;

internal static class EngagementAccess
{
    public static bool Actor(HttpContext context, out Guid actorId) =>
        Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out actorId);

    public static bool IsAdmin(HttpContext context) => context.User.IsInRole(CarpediemRoles.Admin);

    public static async Task<bool> LeadsAsync(CarpediemDbContext db, Guid eventId, Guid actorId,
        CancellationToken cancellationToken) =>
        await db.ChiefLeaderAssignments.AsNoTracking().AnyAsync(assignment =>
            assignment.EventId == eventId && assignment.ChiefLeaderUserId == actorId &&
            assignment.Status == ChiefLeaderAssignmentStatus.Active, cancellationToken);

    public static async Task<bool> AssignedAsync(CarpediemDbContext db, Guid eventId, Guid actorId,
        CancellationToken cancellationToken) =>
        await (from assignment in db.TeamAssignments.AsNoTracking()
               join cycle in db.StaffingCycles.AsNoTracking() on assignment.StaffingCycleId equals cycle.Id
               where assignment.EventId == eventId && assignment.UserId == actorId &&
                     assignment.Status == TeamAssignmentStatus.Active && cycle.IsCurrent
               select assignment.Id).AnyAsync(cancellationToken);
}
