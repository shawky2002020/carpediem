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

    public void AcceptDirect(Guid actorUserId, DateTimeOffset acceptedAt,
        bool hasActiveTeamAssignmentOnCurrentCycle)
    {
        ValidateDirectResponse(actorUserId, acceptedAt);
        if (hasActiveTeamAssignmentOnCurrentCycle) throw new ActiveTeamAssignmentConflictException();
        Status = ChiefLeaderAssignmentStatus.Active;
        AcceptedAt = acceptedAt;
        AcceptedByUserId = actorUserId;
        AcceptanceSource = "Direct";
    }

    public void DeclineDirect(Guid actorUserId, DateTimeOffset declinedAt)
    {
        ValidateDirectResponse(actorUserId, declinedAt);
        Status = ChiefLeaderAssignmentStatus.Declined;
        DeclinedAt = declinedAt;
    }

    public void AcceptExternal(Guid adminUserId, DateTimeOffset acceptedAt,
        bool hasActiveTeamAssignmentOnCurrentCycle)
    {
        if (adminUserId == Guid.Empty)
            throw new ArgumentException("An acting Admin is required.", nameof(adminUserId));
        ValidatePendingUtc(acceptedAt);
        if (hasActiveTeamAssignmentOnCurrentCycle) throw new ActiveTeamAssignmentConflictException();
        Status = ChiefLeaderAssignmentStatus.Active;
        AcceptedAt = acceptedAt;
        AcceptedByUserId = adminUserId;
        AcceptanceSource = "AdminExternal";
    }

    private void ValidateDirectResponse(Guid actorUserId, DateTimeOffset respondedAt)
    {
        if (actorUserId != ChiefLeaderUserId)
            throw new UnauthorizedAccessException("Only the appointed candidate may respond.");
        ValidatePendingUtc(respondedAt);
    }

    private void ValidatePendingUtc(DateTimeOffset respondedAt)
    {
        if (Status != ChiefLeaderAssignmentStatus.Pending)
            throw new ChiefLeaderResponseStateException();
        if (respondedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Response time must be UTC.", nameof(respondedAt));
    }

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
