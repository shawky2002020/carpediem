namespace Carpediem.Modules.Events.Application.Reschedule;

public sealed record RescheduleEventResult(
    Guid EventId,
    string Title,
    string Status,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    string? Notes,
    Guid ActiveStaffingCycleId,
    int CycleNumber,
    byte[] Version,
    DateTimeOffset RescheduledAt,
    int NotificationsDispatched);

public interface IEventRescheduleStore
{
    Task<RescheduleEventResult> RescheduleEventAsync(
        Guid eventId,
        DateTimeOffset newStartDateTime,
        DateTimeOffset newEndDateTime,
        string justification,
        byte[] expectedVersion,
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken);
}
