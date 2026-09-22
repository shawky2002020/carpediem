namespace Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;

public sealed class ChiefLeaderAssignment
{
    private ChiefLeaderAssignment() { }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid ChiefLeaderUserId { get; private set; }
    public ChiefLeaderAssignmentStatus Status { get; private set; }
    public Guid AppointedByUserId { get; private set; }
    public DateTimeOffset AppointedAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public string? AcceptanceSource { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTimeOffset? DeclinedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    internal static ChiefLeaderAssignment CreatePending(
        Guid eventId,
        Guid chiefLeaderUserId,
        Guid appointedByUserId,
        DateTimeOffset appointedAt)
    {
        if (eventId == Guid.Empty) throw new ArgumentException("An Event is required.", nameof(eventId));
        if (chiefLeaderUserId == Guid.Empty) throw new ArgumentException("A candidate is required.", nameof(chiefLeaderUserId));
        if (appointedByUserId == Guid.Empty) throw new ArgumentException("An appointing Admin is required.", nameof(appointedByUserId));
        if (appointedAt.Offset != TimeSpan.Zero) throw new ArgumentException("Appointment time must be UTC.", nameof(appointedAt));

        return new ChiefLeaderAssignment
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            ChiefLeaderUserId = chiefLeaderUserId,
            Status = ChiefLeaderAssignmentStatus.Pending,
            AppointedByUserId = appointedByUserId,
            AppointedAt = appointedAt,
        };
    }
}
