using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;

namespace Carpediem.Modules.Events.Application.ChiefLeaderAppointments;

public sealed record AppointmentCandidate(Guid UserId, string UserName);

public sealed record ChiefLeaderAppointmentView(
    Guid Id,
    Guid EventId,
    AppointmentCandidate Candidate,
    ChiefLeaderAssignmentStatus Status,
    DateTimeOffset AppointedAt,
    Guid AppointedByUserId,
    byte[] Version);

public sealed record ChiefLeaderAppointmentContext(
    Guid EventId,
    string EventTitle,
    string EventStatus,
    ChiefLeaderAppointmentView? CurrentAppointment,
    IReadOnlyList<ChiefLeaderAppointmentView> DeclinedHistory,
    IReadOnlyList<AppointmentCandidate> Candidates);

public sealed record ChiefLeaderAppointmentStoreResult(
    ChiefLeaderAppointmentView Appointment,
    bool IsReplay);

public sealed class ChiefLeaderAppointmentNotFoundException : Exception;
public sealed class CandidateIneligibleException : Exception;
public sealed class AppointmentRequestConflictException : Exception;
public sealed class AppointmentConflictException : Exception;

public interface IChiefLeaderAppointmentStore
{
    Task<ChiefLeaderAppointmentContext> GetContextAsync(Guid eventId, Guid actorUserId,
        CancellationToken cancellationToken);

    Task<ChiefLeaderAppointmentStoreResult> AppointAsync(
        Guid eventId,
        Guid candidateUserId,
        Guid actorUserId,
        Guid requestToken,
        byte[] fingerprint,
        Guid correlationId,
        DateTimeOffset appointedAt,
        CancellationToken cancellationToken);
}
