namespace Carpediem.Modules.Events.Domain.StaffingCycles;

public sealed class StaffingCycle
{
    private StaffingCycle() { }

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public int CycleNumber { get; private set; }
    public DateOnly EffectiveEventDate { get; private set; }
    public bool IsCurrent { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    internal static StaffingCycle CreateInitial(Guid eventId, DateOnly effectiveEventDate, DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            CycleNumber = 1,
            EffectiveEventDate = effectiveEventDate,
            IsCurrent = true,
            CreatedAt = createdAt,
        };

    internal static StaffingCycle CreateNext(Guid eventId, int cycleNumber, DateOnly effectiveEventDate, DateTimeOffset createdAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            CycleNumber = cycleNumber,
            EffectiveEventDate = effectiveEventDate,
            IsCurrent = true,
            CreatedAt = createdAt,
        };

    internal void MarkHistorical() => IsCurrent = false;
}
