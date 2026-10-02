using Carpediem.Modules.Staffing.Domain.Availability;

namespace Carpediem.Modules.Staffing.Domain.TeamSelection;

public sealed class TeamAssignment
{
    private TeamAssignment() { }

    public Guid Id { get; private set; }
    public Guid StaffingCycleId { get; private set; }
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public TeamAssignmentStatus Status { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public Guid AssignedByUserId { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static TeamAssignment Create(
        Guid staffingCycleId,
        Guid eventId,
        Guid candidateUserId,
        Guid assignedByUserId,
        DateTimeOffset eventStartUtc,
        DateTimeOffset assignedAtUtc,
        string? candidateAvailabilityResponse,
        bool isCandidateChiefLeaderOnEvent,
        string eventStatus)
    {
        if (staffingCycleId == Guid.Empty) throw new ArgumentException("StaffingCycleId cannot be empty.", nameof(staffingCycleId));
        if (eventId == Guid.Empty) throw new ArgumentException("EventId cannot be empty.", nameof(eventId));
        if (candidateUserId == Guid.Empty) throw new ArgumentException("CandidateUserId cannot be empty.", nameof(candidateUserId));
        if (assignedByUserId == Guid.Empty) throw new ArgumentException("AssignedByUserId cannot be empty.", nameof(assignedByUserId));
        if (assignedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("AssignedAt must be UTC.", nameof(assignedAtUtc));
        if (eventStartUtc.Offset != TimeSpan.Zero) throw new ArgumentException("EventStart must be UTC.", nameof(eventStartUtc));

        if (!string.Equals(eventStatus, "Published", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidEventStatusException(eventId, eventStatus);
        }

        if (eventStartUtc <= assignedAtUtc)
        {
            throw new TeamSelectionWindowClosedException(eventId);
        }

        if (isCandidateChiefLeaderOnEvent)
        {
            throw new ChiefLeaderExclusivityException(eventId, candidateUserId);
        }

        if (!string.Equals(candidateAvailabilityResponse, "Available", StringComparison.OrdinalIgnoreCase))
        {
            throw new CandidateNotAvailableException(eventId, candidateUserId);
        }

        return new TeamAssignment
        {
            Id = Guid.NewGuid(),
            StaffingCycleId = staffingCycleId,
            EventId = eventId,
            UserId = candidateUserId,
            Status = TeamAssignmentStatus.Active,
            AssignedAt = assignedAtUtc,
            AssignedByUserId = assignedByUserId
        };
    }

    public static TeamAssignment CreateDirectAssignment(
        Guid staffingCycleId,
        Guid eventId,
        Guid candidateUserId,
        Guid assignedByUserId,
        DateTimeOffset eventStartUtc,
        DateTimeOffset assignedAtUtc,
        bool isCandidateChiefLeaderOnEvent,
        string eventStatus)
    {
        if (staffingCycleId == Guid.Empty) throw new ArgumentException("StaffingCycleId cannot be empty.", nameof(staffingCycleId));
        if (eventId == Guid.Empty) throw new ArgumentException("EventId cannot be empty.", nameof(eventId));
        if (candidateUserId == Guid.Empty) throw new ArgumentException("CandidateUserId cannot be empty.", nameof(candidateUserId));
        if (assignedByUserId == Guid.Empty) throw new ArgumentException("AssignedByUserId cannot be empty.", nameof(assignedByUserId));
        if (assignedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("AssignedAt must be UTC.", nameof(assignedAtUtc));
        if (eventStartUtc.Offset != TimeSpan.Zero) throw new ArgumentException("EventStart must be UTC.", nameof(eventStartUtc));

        if (!string.Equals(eventStatus, "Published", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidEventStatusException(eventId, eventStatus);
        }

        if (eventStartUtc <= assignedAtUtc)
        {
            throw new TeamSelectionWindowClosedException(eventId);
        }

        if (isCandidateChiefLeaderOnEvent)
        {
            throw new ChiefLeaderExclusivityException(eventId, candidateUserId);
        }

        return new TeamAssignment
        {
            Id = Guid.NewGuid(),
            StaffingCycleId = staffingCycleId,
            EventId = eventId,
            UserId = candidateUserId,
            Status = TeamAssignmentStatus.Active,
            AssignedAt = assignedAtUtc,
            AssignedByUserId = assignedByUserId
        };
    }

    public static TeamAssignment CreateReplacementAssignment(
        Guid staffingCycleId,
        Guid eventId,
        Guid candidateUserId,
        Guid assignedByUserId,
        DateTimeOffset eventStartUtc,
        DateTimeOffset assignedAtUtc,
        bool isCandidateChiefLeaderOnEvent,
        string eventStatus)
    {
        if (staffingCycleId == Guid.Empty) throw new ArgumentException("StaffingCycleId cannot be empty.", nameof(staffingCycleId));
        if (eventId == Guid.Empty) throw new ArgumentException("EventId cannot be empty.", nameof(eventId));
        if (candidateUserId == Guid.Empty) throw new ArgumentException("CandidateUserId cannot be empty.", nameof(candidateUserId));
        if (assignedByUserId == Guid.Empty) throw new ArgumentException("AssignedByUserId cannot be empty.", nameof(assignedByUserId));
        if (assignedAtUtc.Offset != TimeSpan.Zero) throw new ArgumentException("AssignedAt must be UTC.", nameof(assignedAtUtc));
        if (eventStartUtc.Offset != TimeSpan.Zero) throw new ArgumentException("EventStart must be UTC.", nameof(eventStartUtc));

        if (!string.Equals(eventStatus, "Published", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidEventStatusException(eventId, eventStatus);
        }

        if (eventStartUtc <= assignedAtUtc)
        {
            throw new TeamSelectionWindowClosedException(eventId);
        }

        if (isCandidateChiefLeaderOnEvent)
        {
            throw new ChiefLeaderExclusivityException(eventId, candidateUserId);
        }

        return new TeamAssignment
        {
            Id = Guid.NewGuid(),
            StaffingCycleId = staffingCycleId,
            EventId = eventId,
            UserId = candidateUserId,
            Status = TeamAssignmentStatus.Active,
            AssignedAt = assignedAtUtc,
            AssignedByUserId = assignedByUserId
        };
    }

    public void Cancel(
        string justification,
        DateTimeOffset cancelledAtUtc,
        DateTimeOffset eventStartUtc)
    {
        if (cancelledAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("CancelledAt must be UTC.", nameof(cancelledAtUtc));
        if (eventStartUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("EventStart must be UTC.", nameof(eventStartUtc));

        if (string.IsNullOrWhiteSpace(justification))
            throw new ArgumentException("A non-empty justification is required for cancellation.", nameof(justification));

        var trimmed = justification.Trim();
        if (trimmed.Length > 500)
            throw new ArgumentException("Cancellation justification cannot exceed 500 characters.", nameof(justification));

        if (Status == TeamAssignmentStatus.Cancelled)
            throw new AssignmentAlreadyCancelledException(Id);

        if (Status != TeamAssignmentStatus.Active)
            throw new InvalidOperationException($"Team assignment '{Id}' cannot be cancelled because its status is '{Status}'.");

        if (eventStartUtc <= cancelledAtUtc)
            throw new CancellationWindowClosedException(EventId);

        Status = TeamAssignmentStatus.Cancelled;
        CancellationReason = trimmed;
        CancelledAt = cancelledAtUtc;
    }
}
