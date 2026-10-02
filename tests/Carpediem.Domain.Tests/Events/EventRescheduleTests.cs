using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;

namespace Carpediem.Domain.Tests.Events;

public sealed class EventRescheduleTests
{
    [Fact]
    public void RescheduleUpdatesDatesAndRotatesStaffingCycle()
    {
        var publishedEvent = CreatePublishedEvent();
        var adminId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var newStart = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero);
        var newEnd = new DateTimeOffset(2026, 11, 20, 17, 0, 0, TimeSpan.Zero);

        var newCycle = publishedEvent.Reschedule(newStart, newEnd, adminId, now);

        Assert.Equal(EventStatus.Published, publishedEvent.Status);
        Assert.Equal(newStart, publishedEvent.StartDateTime);
        Assert.Equal(newEnd, publishedEvent.EndDateTime);

        // Verification of cycle rotation
        Assert.Equal(2, newCycle.CycleNumber);
        Assert.True(newCycle.IsCurrent);
        Assert.Equal(new DateOnly(2026, 11, 20), newCycle.EffectiveEventDate);
        Assert.Equal(now, newCycle.CreatedAt);

        // Historical cycle is sealed
        var initialCycle = publishedEvent.StaffingCycles.Single(c => c.CycleNumber == 1);
        Assert.False(initialCycle.IsCurrent);

        // Exactly one cycle is current
        Assert.Single(publishedEvent.StaffingCycles, c => c.IsCurrent);
    }

    [Fact]
    public void RescheduleThrowsWhenEventIsNotPublished()
    {
        var start = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var draft = Event.CreateDraft("Draft Event", start, start.AddHours(4), "Main Hall", null,
            Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2026, 11, 1));
        var adminId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var newStart = start.AddDays(7);
        var newEnd = newStart.AddHours(4);

        var ex = Assert.Throws<InvalidEventStatusForRescheduleException>(() =>
            draft.Reschedule(newStart, newEnd, adminId, now));

        Assert.Equal(draft.Id, ex.EventId);
        Assert.Equal("Draft", ex.CurrentStatus);
    }

    [Fact]
    public void RescheduleThrowsWhenEventAlreadyStarted()
    {
        var publishedEvent = CreatePublishedEvent(startDateTime: new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero));
        var adminId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero); // Event started yesterday
        var newStart = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var newEnd = newStart.AddHours(4);

        var ex = Assert.Throws<EventRescheduleWindowClosedException>(() =>
            publishedEvent.Reschedule(newStart, newEnd, adminId, now));

        Assert.Equal(publishedEvent.Id, ex.EventId);
    }

    [Fact]
    public void RescheduleThrowsWhenNewStartIsInPast()
    {
        var publishedEvent = CreatePublishedEvent();
        var adminId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var pastNewStart = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
        var pastNewEnd = pastNewStart.AddHours(4);

        Assert.Throws<ArgumentException>(() =>
            publishedEvent.Reschedule(pastNewStart, pastNewEnd, adminId, now));
    }

    [Fact]
    public void RescheduleThrowsWhenNewEndIsBeforeNewStart()
    {
        var publishedEvent = CreatePublishedEvent();
        var adminId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var newStart = new DateTimeOffset(2026, 11, 1, 14, 0, 0, TimeSpan.Zero);
        var invalidNewEnd = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() =>
            publishedEvent.Reschedule(newStart, invalidNewEnd, adminId, now));
    }

    [Fact]
    public void RescheduleThrowsWhenTimestampsAreNotUtc()
    {
        var publishedEvent = CreatePublishedEvent();
        var adminId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var nonUtcStart = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.FromHours(2));
        var utcEnd = new DateTimeOffset(2026, 11, 1, 13, 0, 0, TimeSpan.Zero);

        Assert.Throws<ArgumentException>(() =>
            publishedEvent.Reschedule(nonUtcStart, utcEnd, adminId, now));
    }

    [Fact]
    public void RescheduleThrowsWhenActorIsMissing()
    {
        var publishedEvent = CreatePublishedEvent();
        var now = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var newStart = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var newEnd = newStart.AddHours(4);

        Assert.Throws<ArgumentException>(() =>
            publishedEvent.Reschedule(newStart, newEnd, Guid.Empty, now));
    }

    [Fact]
    public void MultipleReschedulesIncrementCycleNumbersSequentially()
    {
        var publishedEvent = CreatePublishedEvent();
        var adminId = Guid.NewGuid();
        var t1 = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);
        var s1 = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var e1 = s1.AddHours(4);

        var cycle2 = publishedEvent.Reschedule(s1, e1, adminId, t1);
        Assert.Equal(2, cycle2.CycleNumber);
        Assert.True(cycle2.IsCurrent);

        var t2 = new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);
        var s2 = new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.Zero);
        var e2 = s2.AddHours(4);

        var cycle3 = publishedEvent.Reschedule(s2, e2, adminId, t2);
        Assert.Equal(3, cycle3.CycleNumber);
        Assert.True(cycle3.IsCurrent);
        Assert.False(cycle2.IsCurrent);

        Assert.Equal(3, publishedEvent.StaffingCycles.Count);
        Assert.Single(publishedEvent.StaffingCycles, c => c.IsCurrent);
    }

    private static Event CreatePublishedEvent(DateTimeOffset? startDateTime = null)
    {
        var start = startDateTime ?? new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var draft = Event.CreateDraft("Gala Event", start, start.AddHours(4), "Main Hall", null,
            Guid.NewGuid(), DateTimeOffset.UtcNow, DateOnly.FromDateTime(start.Date));
        var candidateId = Guid.NewGuid();
        var assignment = draft.AppointChiefLeader(candidateId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        assignment.AcceptDirect(candidateId, DateTimeOffset.UtcNow, hasActiveTeamAssignmentOnCurrentCycle: false);
        draft.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow);
        return draft;
    }
}
