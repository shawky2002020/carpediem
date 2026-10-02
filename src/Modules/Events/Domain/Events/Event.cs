using Carpediem.Modules.Events.Domain.StaffingCycles;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using System.Diagnostics.CodeAnalysis;

namespace Carpediem.Modules.Events.Domain.Events;

public sealed class NotDraftException : Exception
{
    public NotDraftException() : base("The Event is not a Draft.") { }
}

public sealed record DraftEditChanges(
    string PreviousTitle,
    DateTimeOffset PreviousStartDateTime,
    DateTimeOffset PreviousEndDateTime,
    string PreviousLocation,
    bool NotesChanged);

[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Event is the locked Carpediem domain aggregate name.")]
public sealed class Event
{
    private readonly List<StaffingCycle> staffingCycles = [];
    private readonly List<ChiefLeaderAssignment> chiefLeaderAssignments = [];

    private Event() { }

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public DateTimeOffset StartDateTime { get; private set; }
    public DateTimeOffset EndDateTime { get; private set; }
    public string Location { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public EventStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public byte[] RowVersion { get; private set; } = [];
    public IReadOnlyCollection<StaffingCycle> StaffingCycles => staffingCycles.AsReadOnly();
    public IReadOnlyCollection<ChiefLeaderAssignment> ChiefLeaderAssignments => chiefLeaderAssignments.AsReadOnly();

    public ChiefLeaderAssignment AppointChiefLeader(
        Guid chiefLeaderUserId,
        Guid appointedByUserId,
        DateTimeOffset appointedAt)
    {
        if (Status != EventStatus.Draft) throw new NotDraftException();
        if (chiefLeaderAssignments.Any(item => item.Status is
            ChiefLeaderAssignmentStatus.Pending or ChiefLeaderAssignmentStatus.Active))
        {
            throw new ChiefLeaderAlreadyAssignedException();
        }

        var assignment = ChiefLeaderAssignment.CreatePending(Id, chiefLeaderUserId,
            appointedByUserId, appointedAt);
        chiefLeaderAssignments.Add(assignment);
        return assignment;
    }

    public void Publish(Guid publishedByUserId, DateTimeOffset publishedAt)
    {
        if (Status != EventStatus.Draft) throw new NotDraftException();
        if (publishedByUserId == Guid.Empty)
            throw new ArgumentException("An acting Admin is required.", nameof(publishedByUserId));
        if (publishedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Publication time must be UTC.", nameof(publishedAt));
        if (StartDateTime >= EndDateTime)
            throw new InvalidOperationException("End time must be after start time.");
        if (chiefLeaderAssignments.Count(assignment => assignment.Status == ChiefLeaderAssignmentStatus.Active) != 1)
            throw new ActiveChiefLeaderRequiredException();

        Status = EventStatus.Published;
    }

    public StaffingCycle Reschedule(
        DateTimeOffset newStartDateTime,
        DateTimeOffset newEndDateTime,
        Guid actorUserId,
        DateTimeOffset rescheduledAt)
    {
        if (Status != EventStatus.Published)
            throw new InvalidEventStatusForRescheduleException(Id, Status.ToString());
        if (StartDateTime <= rescheduledAt)
            throw new EventRescheduleWindowClosedException(Id);
        if (actorUserId == Guid.Empty)
            throw new ArgumentException("An acting Admin is required.", nameof(actorUserId));
        if (rescheduledAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Reschedule time must be UTC.", nameof(rescheduledAt));
        if (newStartDateTime.Offset != TimeSpan.Zero)
            throw new ArgumentException("New start time must be UTC.", nameof(newStartDateTime));
        if (newEndDateTime.Offset != TimeSpan.Zero)
            throw new ArgumentException("New end time must be UTC.", nameof(newEndDateTime));
        if (newStartDateTime <= rescheduledAt)
            throw new ArgumentException("New start time must be in the future.", nameof(newStartDateTime));
        if (newStartDateTime >= newEndDateTime)
            throw new ArgumentException("New end time must be after the new start time.", nameof(newEndDateTime));

        StartDateTime = newStartDateTime;
        EndDateTime = newEndDateTime;

        var currentCycle = staffingCycles.Single(cycle => cycle.IsCurrent);
        currentCycle.MarkHistorical();

        var effectiveEventDate = DateOnly.FromDateTime(newStartDateTime.Date);
        var nextCycle = StaffingCycle.CreateNext(
            Id,
            staffingCycles.Max(c => c.CycleNumber) + 1,
            effectiveEventDate,
            rescheduledAt);

        staffingCycles.Add(nextCycle);
        return nextCycle;
    }

    public void Complete(DateTimeOffset completedAt, Guid? actorUserId = null)
    {
        if (Status != EventStatus.Published)
            throw new InvalidEventStatusForCompletionException(Id, Status.ToString());

        if (completedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Completion time must be UTC.", nameof(completedAt));

        Status = EventStatus.Completed;
    }

    public static Event CreateDraft(
        string? title,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        string? location,
        string? notes,
        Guid createdByUserId,
        DateTimeOffset createdAt,
        DateOnly effectiveEventDate)
    {
        ValidateDraftValues(title, startDateTime, endDateTime, location);
        if (createdByUserId == Guid.Empty) throw new ArgumentException("A creator is required.", nameof(createdByUserId));
        if (createdAt.Offset != TimeSpan.Zero) throw new ArgumentException("Creation time must be UTC.", nameof(createdAt));

        var draft = new Event
        {
            Id = Guid.NewGuid(),
            Title = title!,
            StartDateTime = startDateTime,
            EndDateTime = endDateTime,
            Location = location!,
            Notes = notes,
            Status = EventStatus.Draft,
            CreatedByUserId = createdByUserId,
            CreatedAt = createdAt,
        };
        draft.staffingCycles.Add(StaffingCycle.CreateInitial(draft.Id, effectiveEventDate, createdAt));
        return draft;
    }

    public DraftEditChanges EditDraft(
        string? title,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        string? location,
        string? notes,
        DateOnly effectiveEventDate,
        DateTimeOffset editedAt)
    {
        if (Status != EventStatus.Draft) throw new NotDraftException();
        ValidateDraftValues(title, startDateTime, endDateTime, location);
        if (editedAt.Offset != TimeSpan.Zero) throw new ArgumentException("Edit time must be UTC.", nameof(editedAt));
        var currentCycle = staffingCycles.Single(cycle => cycle.IsCurrent);

        var changes = new DraftEditChanges(Title, StartDateTime, EndDateTime, Location,
            !string.Equals(Notes, notes, StringComparison.Ordinal));
        Title = title!;
        StartDateTime = startDateTime;
        EndDateTime = endDateTime;
        Location = location!;
        Notes = notes;

        if (currentCycle.EffectiveEventDate != effectiveEventDate)
        {
            currentCycle.MarkHistorical();
            staffingCycles.Add(StaffingCycle.CreateNext(Id,
                staffingCycles.Max(cycle => cycle.CycleNumber) + 1, effectiveEventDate, editedAt));
        }

        return changes;
    }

    private static void ValidateDraftValues(
        string? title,
        DateTimeOffset startDateTime,
        DateTimeOffset endDateTime,
        string? location)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(title)) errors["title"] = ["Title is required."];
        else if (title.Length > 200) errors["title"] = ["Title exceeds the supported length."];
        if (string.IsNullOrWhiteSpace(location)) errors["location"] = ["Location is required."];
        if (startDateTime.Offset != TimeSpan.Zero) errors["start"] = ["Start time must resolve to UTC."];
        if (endDateTime.Offset != TimeSpan.Zero) errors["end"] = ["End time must resolve to UTC."];
        if (startDateTime >= endDateTime) errors["end"] = ["End time must be after start time."];
        if (errors.Count > 0) throw new DraftValidationException(errors);
    }
}
