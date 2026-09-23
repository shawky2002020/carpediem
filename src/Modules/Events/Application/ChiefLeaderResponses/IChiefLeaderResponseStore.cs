using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;

namespace Carpediem.Modules.Events.Application.ChiefLeaderResponses;

public sealed record ChiefLeaderResponseView(
    Guid Id,
    Guid EventId,
    string EventTitle,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    ChiefLeaderAssignmentStatus Status,
    DateTimeOffset AppointedAt,
    DateTimeOffset? AcceptedAt,
    DateTimeOffset? DeclinedAt,
    byte[] Version);

public enum ChiefLeaderDecision { Accept, Decline }

public sealed class ChiefLeaderResponseNotFoundException : Exception;
public sealed class ChiefLeaderResponseConflictException : Exception;

public interface IChiefLeaderResponseStore
{
    Task<IReadOnlyList<ChiefLeaderResponseView>> ListPendingAsync(
        Guid actorUserId, CancellationToken cancellationToken);

    Task<ChiefLeaderResponseView> GetAsync(
        Guid assignmentId, Guid actorUserId, CancellationToken cancellationToken);

    Task<ChiefLeaderResponseView> RespondAsync(
        Guid assignmentId, Guid actorUserId, byte[] expectedVersion,
        ChiefLeaderDecision decision, Guid correlationId, DateTimeOffset respondedAt,
        CancellationToken cancellationToken);
}
