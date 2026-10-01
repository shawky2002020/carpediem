using Carpediem.Modules.Events.Application.ChiefLeaderAppointments;

namespace Carpediem.Modules.Events.Application.ChiefLeaderExternalAcceptances;

public sealed record ChiefLeaderExternalAcceptanceResult(
    ChiefLeaderAppointmentView Appointment,
    bool IsReplay);

public sealed class ChiefLeaderExternalAcceptanceNotFoundException : Exception;
public sealed class ChiefLeaderExternalAcceptanceConflictException : Exception;
public sealed class ChiefLeaderExternalAcceptanceValidationException : Exception
{
    public ChiefLeaderExternalAcceptanceValidationException(string message) : base(message) { }
}

public interface IChiefLeaderExternalAcceptanceStore
{
    Task<ChiefLeaderExternalAcceptanceResult> RecordAsync(
        Guid eventId,
        Guid assignmentId,
        Guid actorUserId,
        byte[] expectedVersion,
        string justification,
        Guid correlationId,
        DateTimeOffset acceptedAt,
        CancellationToken cancellationToken);
}
