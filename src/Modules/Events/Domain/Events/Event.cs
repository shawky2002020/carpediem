using Carpediem.Modules.Events.Domain.StaffingCycles;
using System.Diagnostics.CodeAnalysis;

namespace Carpediem.Modules.Events.Domain.Events;

[SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Event is the locked Carpediem domain aggregate name.")]
public sealed class Event
{
    private readonly List<StaffingCycle> staffingCycles = [];

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
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(title)) errors["title"] = ["Title is required."];
        else if (title.Length > 200) errors["title"] = ["Title exceeds the supported length."];
        if (string.IsNullOrWhiteSpace(location)) errors["location"] = ["Location is required."];
        if (startDateTime.Offset != TimeSpan.Zero) errors["start"] = ["Start time must resolve to UTC."];
        if (endDateTime.Offset != TimeSpan.Zero) errors["end"] = ["End time must resolve to UTC."];
        if (startDateTime >= endDateTime) errors["end"] = ["End time must be after start time."];
        if (errors.Count > 0) throw new DraftValidationException(errors);
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
}
