namespace Carpediem.Modules.Attendance.Domain;

public enum AttendanceOutcomeValue
{
    Attended = 1,
    NoShow = 2
}

public sealed class AttendanceOutcome
{
    private AttendanceOutcomeValue _outcome;

    private AttendanceOutcome() { }

    public Guid Id { get; private set; }
    public Guid TeamAssignmentId { get; private set; }
    public AttendanceOutcomeValue Outcome => _outcome;
    public Guid FinalizedByUserId { get; private set; }
    public DateTimeOffset FinalizedAt { get; private set; }
    public Guid? CorrectedByUserId { get; private set; }
    public DateTimeOffset? CorrectedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static AttendanceOutcome Create(
        Guid teamAssignmentId,
        AttendanceOutcomeValue outcome,
        Guid finalizedByUserId,
        DateTimeOffset finalizedAt)
    {
        if (teamAssignmentId == Guid.Empty)
        {
            throw new ArgumentException("TeamAssignmentId cannot be empty.", nameof(teamAssignmentId));
        }

        if (finalizedByUserId == Guid.Empty)
        {
            throw new ArgumentException("FinalizedByUserId cannot be empty.", nameof(finalizedByUserId));
        }

        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome), "Invalid attendance outcome value.");
        }

        return new AttendanceOutcome
        {
            Id = Guid.NewGuid(),
            TeamAssignmentId = teamAssignmentId,
            _outcome = outcome,
            FinalizedByUserId = finalizedByUserId,
            FinalizedAt = finalizedAt,
        };
    }

    public void UpdateOutcome(AttendanceOutcomeValue newOutcome, Guid updatedByUserId, DateTimeOffset updatedAt)
    {
        if (!Enum.IsDefined(newOutcome))
        {
            throw new ArgumentOutOfRangeException(nameof(newOutcome), "Invalid attendance outcome value.");
        }

        if (updatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("UpdatedByUserId cannot be empty.", nameof(updatedByUserId));
        }

        _outcome = newOutcome;
        FinalizedByUserId = updatedByUserId;
        FinalizedAt = updatedAt;
    }
}
