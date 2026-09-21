using Carpediem.Modules.Events.Domain.Events;

namespace Carpediem.Domain.Tests.Events;

public sealed class EditDraftEventTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 10, 11, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddHours(2);

    [Fact]
    public void ValidEditPreservesDraftStateAndExistingCycleWhenCairoDateIsUnchanged()
    {
        var draft = NewDraft();
        var firstCycleId = Assert.Single(draft.StaffingCycles).Id;

        draft.EditDraft("  Updated  ", Start.AddMinutes(30), End.AddMinutes(30), "  New place  ", "  New notes  ",
            new DateOnly(2026, 10, 10), DateTimeOffset.UtcNow);

        Assert.Equal(EventStatus.Draft, draft.Status);
        Assert.Equal("  Updated  ", draft.Title);
        Assert.Equal("  New place  ", draft.Location);
        Assert.Equal("  New notes  ", draft.Notes);
        Assert.Equal(firstCycleId, Assert.Single(draft.StaffingCycles).Id);
    }

    [Fact]
    public void CairoStartDateChangeArchivesCycleAndCreatesNextCurrentCycle()
    {
        var draft = NewDraft();

        draft.EditDraft("Event", Start.AddDays(1), End.AddDays(1), "Cairo", null,
            new DateOnly(2026, 10, 11), DateTimeOffset.UtcNow);

        Assert.Equal(2, draft.StaffingCycles.Count);
        var previous = Assert.Single(draft.StaffingCycles, cycle => cycle.CycleNumber == 1);
        var current = Assert.Single(draft.StaffingCycles, cycle => cycle.CycleNumber == 2);
        Assert.False(previous.IsCurrent);
        Assert.True(current.IsCurrent);
        Assert.Equal(new DateOnly(2026, 10, 11), current.EffectiveEventDate);
    }

    [Fact]
    public void NonDraftCannotBeEdited()
    {
        var draft = NewDraft();
        typeof(Event).GetProperty(nameof(Event.Status))!.SetValue(draft, EventStatus.Published);

        Assert.Throws<NotDraftException>(() => draft.EditDraft("Changed", Start, End, "Cairo", null,
            new DateOnly(2026, 10, 10), DateTimeOffset.UtcNow));
        Assert.Equal("Event", draft.Title);
    }

    [Theory]
    [InlineData("\u2003", "Cairo", 1)]
    [InlineData("Event", "  ", 1)]
    [InlineData("Event", "Cairo", 0)]
    public void InvalidEditLeavesPreviouslyValidDraftUntouched(string title, string location, int endHours)
    {
        var draft = NewDraft();

        Assert.Throws<DraftValidationException>(() => draft.EditDraft(title, Start, Start.AddHours(endHours),
            location, "Changed", new DateOnly(2026, 10, 11), DateTimeOffset.UtcNow));

        Assert.Equal("Event", draft.Title);
        Assert.Equal("Cairo", draft.Location);
        Assert.Null(draft.Notes);
        Assert.Equal(End, draft.EndDateTime);
        Assert.Single(draft.StaffingCycles);
        Assert.True(Assert.Single(draft.StaffingCycles).IsCurrent);
    }

    private static Event NewDraft() => Event.CreateDraft("Event", Start, End, "Cairo", null,
        Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2026, 10, 10));
}
