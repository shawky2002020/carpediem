namespace Carpediem.Modules.Events.Application.Completion;

public sealed record CompleteEventResult(
    Guid EventId,
    string Title,
    string Status,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    string? Notes,
    DateTimeOffset CompletedAt,
    byte[] Version,
    bool IsReplay = false);

public interface IEventCompletionStore
{
    /// <summary>
    /// Processes all published events whose scheduled EndDateTime is less than or equal to utcNow.
    /// Returns the number of events successfully transitioned to Completed.
    /// </summary>
    Task<int> ProcessScheduledCompletionsAsync(
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually closes out a published event to Completed status by an Administrator.
    /// </summary>
    Task<CompleteEventResult> CompleteEventManuallyAsync(
        Guid eventId,
        byte[] expectedVersion,
        Guid actorUserId,
        Guid correlationId,
        DateTimeOffset utcNow,
        CancellationToken cancellationToken = default);
}
