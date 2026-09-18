using Carpediem.Modules.Events.Domain.Events;

namespace Carpediem.Domain.Tests.Events;

public sealed class CreateDraftEventTests
{
    private static readonly DateTimeOffset Start = new(2025, 1, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddHours(2);

    [Fact]
    public void CreatesDraftWithExactAcceptedTextAndOneCurrentCycle()
    {
        var actor = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);
        var draft = Event.CreateDraft("  Beach cleanup  ", Start, End, "  North shore  ", "  bring gloves  ", actor, createdAt, new DateOnly(2025, 1, 1));

        Assert.NotEqual(Guid.Empty, draft.Id);
        Assert.Equal(EventStatus.Draft, draft.Status);
        Assert.Equal("  Beach cleanup  ", draft.Title);
        Assert.Equal("  North shore  ", draft.Location);
        Assert.Equal("  bring gloves  ", draft.Notes);
        Assert.Equal(Start, draft.StartDateTime);
        Assert.Equal(End, draft.EndDateTime);
        Assert.Equal(actor, draft.CreatedByUserId);
        Assert.Equal(createdAt, draft.CreatedAt);
        var cycle = Assert.Single(draft.StaffingCycles);
        Assert.True(cycle.IsCurrent);
        Assert.Equal(1, cycle.CycleNumber);
        Assert.Equal(draft.Id, cycle.EventId);
        Assert.Equal(new DateOnly(2025, 1, 1), cycle.EffectiveEventDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NotesAreOptional(string? notes)
    {
        var draft = Event.CreateDraft("Event", Start, End, "Cairo", notes, Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2025, 1, 1));
        Assert.Equal(notes, draft.Notes);
    }
}
