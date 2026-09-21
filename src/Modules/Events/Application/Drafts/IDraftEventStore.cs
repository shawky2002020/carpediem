using Carpediem.Modules.Events.Domain.Events;

namespace Carpediem.Modules.Events.Application.Drafts;

public sealed record DraftCreateStoreResult(Event Draft, bool IsReplay);

public sealed class DraftCreateConflictException : Exception
{
    public DraftCreateConflictException() : base("This creation key was already used with different values.") { }
}

public sealed class DraftNotFoundException : Exception
{
    public DraftNotFoundException() : base("Draft Event was not found.") { }
}

public sealed class StaleDraftException : Exception
{
    public StaleDraftException() : base("The Draft has changed since it was opened.") { }
}

public sealed record DraftEditValues(
    string? Title,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string? Location,
    string? Notes,
    DateOnly EffectiveEventDate);

public interface IDraftEventStore
{
    Task<IReadOnlyList<Event>> ListAsync(int page, int pageSize, Guid actorUserId, CancellationToken cancellationToken);
    Task<Event> GetAsync(Guid eventId, Guid actorUserId, CancellationToken cancellationToken);

    Task<DraftCreateStoreResult> CreateAsync(
        Event draft,
        Guid actorUserId,
        Guid requestToken,
        byte[] fingerprint,
        Guid correlationId,
        CancellationToken cancellationToken);

    Task<Event> UpdateAsync(
        Guid eventId,
        DraftEditValues values,
        byte[] expectedVersion,
        Guid actorUserId,
        Guid correlationId,
        CancellationToken cancellationToken);
}
