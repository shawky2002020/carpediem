using Carpediem.Modules.Events.Domain.Events;

namespace Carpediem.Modules.Events.Application.EventPublication;

public sealed record ActiveChiefLeaderInfo(Guid UserId, string UserName, Guid AssignmentId);

public sealed record PublishedEventResult(
    Guid Id,
    string Title,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    string? Notes,
    EventStatus Status,
    ActiveChiefLeaderInfo ActiveChiefLeader,
    byte[] Version,
    DateTimeOffset PublishedAt,
    bool IsReplay);

public sealed class EventPublicationNotFoundException : Exception
{
    public EventPublicationNotFoundException() : base("Draft Event was not found.") { }
}

public sealed class EventPublicationConflictException : Exception
{
    public EventPublicationConflictException(string message) : base(message) { }
}

public sealed class EventPublicationValidationException : Exception
{
    public EventPublicationValidationException(string message) : base(message) { }
}

public interface IEventPublicationStore
{
    Task<PublishedEventResult> PublishAsync(
        Guid eventId,
        Guid actorUserId,
        byte[] expectedVersion,
        Guid correlationId,
        DateTimeOffset publishedAt,
        CancellationToken cancellationToken);
}
