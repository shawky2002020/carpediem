namespace Carpediem.Modules.Notifications.Domain;

public sealed class InPlatformNotification
{
    public const string ChiefLeaderAppointmentType = "ChiefLeaderAppointment";
    public const string ChiefLeaderDeclinedType = "ChiefLeaderDeclined";
    public const string EventPublishedType = "EventPublished";
    public const string VolunteerSelectedType = "VolunteerSelected";
    public const string DirectLeaderAssignedType = "DirectLeaderAssigned";
    public const string TeamMemberCancelledType = "TeamMemberCancelled";
    public const string ReplacementLeaderAssignedType = "ReplacementLeaderAssigned";
    public const string EventRescheduledType = "EventRescheduled";
    public const string EventCancelledType = "EventCancelled";

    private InPlatformNotification() { }

    public Guid Id { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public Guid? EventId { get; private set; }
    public string NotificationType { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ReadAt { get; private set; }
    public string? ContextPayload { get; private set; }

    public void MarkRead(DateTimeOffset readAt)
    {
        if (ReadAt is not null) return;
        if (readAt.Offset != TimeSpan.Zero || readAt < CreatedAt)
            throw new ArgumentException("Read time must be UTC and no earlier than creation.", nameof(readAt));
        ReadAt = readAt;
    }

    public static InPlatformNotification ChiefLeaderAppointment(
        Guid recipientUserId,
        Guid eventId,
        DateTimeOffset createdAt,
        string contextPayload)
    {
        if (recipientUserId == Guid.Empty) throw new ArgumentException("A recipient is required.", nameof(recipientUserId));
        if (eventId == Guid.Empty) throw new ArgumentException("An Event is required.", nameof(eventId));
        if (createdAt.Offset != TimeSpan.Zero) throw new ArgumentException("Creation time must be UTC.", nameof(createdAt));
        if (contextPayload.Length > 500) throw new ArgumentException("Notification context is too long.", nameof(contextPayload));

        return new InPlatformNotification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            EventId = eventId,
            NotificationType = ChiefLeaderAppointmentType,
            CreatedAt = createdAt,
            ContextPayload = contextPayload,
        };
    }

    public static InPlatformNotification ChiefLeaderDeclined(
        Guid recipientUserId, Guid eventId, DateTimeOffset createdAt, string contextPayload)
    {
        var notification = ChiefLeaderAppointment(recipientUserId, eventId, createdAt, contextPayload);
        notification.NotificationType = ChiefLeaderDeclinedType;
        return notification;
    }

    public static InPlatformNotification EventPublished(
        Guid recipientUserId,
        Guid eventId,
        DateTimeOffset createdAt,
        string contextPayload)
    {
        if (recipientUserId == Guid.Empty) throw new ArgumentException("A recipient is required.", nameof(recipientUserId));
        if (eventId == Guid.Empty) throw new ArgumentException("An Event is required.", nameof(eventId));
        if (createdAt.Offset != TimeSpan.Zero) throw new ArgumentException("Creation time must be UTC.", nameof(createdAt));
        if (contextPayload.Length > 500) throw new ArgumentException("Notification context is too long.", nameof(contextPayload));

        return new InPlatformNotification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            EventId = eventId,
            NotificationType = EventPublishedType,
            CreatedAt = createdAt,
            ContextPayload = contextPayload,
        };
    }

    public static InPlatformNotification VolunteerSelected(
        Guid recipientUserId,
        Guid eventId,
        DateTimeOffset createdAt,
        string contextPayload)
    {
        if (recipientUserId == Guid.Empty) throw new ArgumentException("A recipient is required.", nameof(recipientUserId));
        if (eventId == Guid.Empty) throw new ArgumentException("An Event is required.", nameof(eventId));
        if (createdAt.Offset != TimeSpan.Zero) throw new ArgumentException("Creation time must be UTC.", nameof(createdAt));
        if (contextPayload.Length > 500) throw new ArgumentException("Notification context is too long.", nameof(contextPayload));

        return new InPlatformNotification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            EventId = eventId,
            NotificationType = VolunteerSelectedType,
            CreatedAt = createdAt,
            ContextPayload = contextPayload,
        };
    }

    public static InPlatformNotification DirectLeaderAssigned(
        Guid recipientUserId,
        Guid eventId,
        DateTimeOffset createdAt,
        string contextPayload)
    {
        if (recipientUserId == Guid.Empty) throw new ArgumentException("A recipient is required.", nameof(recipientUserId));
        if (eventId == Guid.Empty) throw new ArgumentException("An Event is required.", nameof(eventId));
        if (createdAt.Offset != TimeSpan.Zero) throw new ArgumentException("Creation time must be UTC.", nameof(createdAt));
        if (contextPayload.Length > 500) throw new ArgumentException("Notification context is too long.", nameof(contextPayload));

        return new InPlatformNotification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            EventId = eventId,
            NotificationType = DirectLeaderAssignedType,
            CreatedAt = createdAt,
            ContextPayload = contextPayload,
        };
    }

    public static InPlatformNotification TeamMemberCancelled(
        Guid recipientUserId,
        Guid eventId,
        DateTimeOffset createdAt,
        string contextPayload)
    {
        if (recipientUserId == Guid.Empty) throw new ArgumentException("A recipient is required.", nameof(recipientUserId));
        if (eventId == Guid.Empty) throw new ArgumentException("An Event is required.", nameof(eventId));
        if (createdAt.Offset != TimeSpan.Zero) throw new ArgumentException("Creation time must be UTC.", nameof(createdAt));
        if (contextPayload.Length > 500) throw new ArgumentException("Notification context is too long.", nameof(contextPayload));

        return new InPlatformNotification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            EventId = eventId,
            NotificationType = TeamMemberCancelledType,
            CreatedAt = createdAt,
            ContextPayload = contextPayload,
        };
    }

    public static InPlatformNotification ReplacementLeaderAssigned(
        Guid recipientUserId,
        Guid eventId,
        DateTimeOffset createdAt,
        string contextPayload)
    {
        if (recipientUserId == Guid.Empty) throw new ArgumentException("A recipient is required.", nameof(recipientUserId));
        if (eventId == Guid.Empty) throw new ArgumentException("An Event is required.", nameof(eventId));
        if (createdAt.Offset != TimeSpan.Zero) throw new ArgumentException("Creation time must be UTC.", nameof(createdAt));
        if (contextPayload.Length > 500) throw new ArgumentException("Notification context is too long.", nameof(contextPayload));

        return new InPlatformNotification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            EventId = eventId,
            NotificationType = ReplacementLeaderAssignedType,
            CreatedAt = createdAt,
            ContextPayload = contextPayload,
        };
    }

    public static InPlatformNotification EventRescheduled(
        Guid recipientUserId,
        Guid eventId,
        DateTimeOffset createdAt,
        string contextPayload)
    {
        if (recipientUserId == Guid.Empty) throw new ArgumentException("A recipient is required.", nameof(recipientUserId));
        if (eventId == Guid.Empty) throw new ArgumentException("An Event is required.", nameof(eventId));
        if (createdAt.Offset != TimeSpan.Zero) throw new ArgumentException("Creation time must be UTC.", nameof(createdAt));
        if (contextPayload.Length > 500) throw new ArgumentException("Notification context is too long.", nameof(contextPayload));

        return new InPlatformNotification
        {
            Id = Guid.NewGuid(),
            RecipientUserId = recipientUserId,
            EventId = eventId,
            NotificationType = EventRescheduledType,
            CreatedAt = createdAt,
            ContextPayload = contextPayload,
        };
    }

    public static InPlatformNotification EventCancelled(
        Guid recipientUserId, Guid eventId, DateTimeOffset createdAt, string contextPayload)
    {
        var notification = ChiefLeaderAppointment(recipientUserId, eventId, createdAt, contextPayload);
        notification.NotificationType = EventCancelledType;
        return notification;
    }
}
