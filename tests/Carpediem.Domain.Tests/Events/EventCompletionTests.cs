using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;

namespace Carpediem.Domain.Tests.Events;

public sealed class EventCompletionTests
{
    [Fact]
    public void CompleteTransitionsPublishedEventToCompleted()
    {
        var publishedEvent = CreatePublishedEvent();
        var adminId = Guid.NewGuid();
        var completedAt = new DateTimeOffset(2026, 11, 2, 18, 0, 0, TimeSpan.Zero);

        publishedEvent.Complete(completedAt, adminId);

        Assert.Equal(EventStatus.Completed, publishedEvent.Status);
        // Verify cycles remain intact
        Assert.Single(publishedEvent.StaffingCycles);
        Assert.True(publishedEvent.StaffingCycles.First().IsCurrent);
    }

    [Fact]
    public void CompleteTransitionsViaSystemWorkerWithoutActor()
    {
        var publishedEvent = CreatePublishedEvent();
        var completedAt = new DateTimeOffset(2026, 11, 2, 18, 0, 0, TimeSpan.Zero);

        publishedEvent.Complete(completedAt, null);

        Assert.Equal(EventStatus.Completed, publishedEvent.Status);
    }

    [Fact]
    public void CompleteThrowsWhenEventIsDraft()
    {
        var start = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var draft = Event.CreateDraft("Draft Event", start, start.AddHours(4), "Main Hall", null,
            Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2026, 11, 1));
        var completedAt = DateTimeOffset.UtcNow;

        var ex = Assert.Throws<InvalidEventStatusForCompletionException>(() =>
            draft.Complete(completedAt));

        Assert.Equal(draft.Id, ex.EventId);
        Assert.Equal("Draft", ex.CurrentStatus);
    }

    [Fact]
    public void CompleteThrowsWhenEventIsAlreadyCompleted()
    {
        var publishedEvent = CreatePublishedEvent();
        var completedAt = new DateTimeOffset(2026, 11, 2, 18, 0, 0, TimeSpan.Zero);
        publishedEvent.Complete(completedAt);

        var ex = Assert.Throws<InvalidEventStatusForCompletionException>(() =>
            publishedEvent.Complete(completedAt.AddMinutes(5)));

        Assert.Equal(publishedEvent.Id, ex.EventId);
        Assert.Equal("Completed", ex.CurrentStatus);
    }

    [Fact]
    public void CompleteThrowsWhenCompletedAtIsNotUtc()
    {
        var publishedEvent = CreatePublishedEvent();
        var nonUtc = new DateTimeOffset(2026, 11, 2, 18, 0, 0, TimeSpan.FromHours(2));

        var ex = Assert.Throws<ArgumentException>(() =>
            publishedEvent.Complete(nonUtc));

        Assert.Equal("completedAt", ex.ParamName);
    }

    private static Event CreatePublishedEvent(
        DateTimeOffset? startDateTime = null,
        DateTimeOffset? endDateTime = null)
    {
        var start = startDateTime ?? new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var end = endDateTime ?? start.AddHours(8);
        var adminId = Guid.NewGuid();
        var leaderId = Guid.NewGuid();
        var effectiveDate = DateOnly.FromDateTime(start.Date);

        var @event = Event.CreateDraft(
            "Gala Fundraiser",
            start,
            end,
            "Main Hall",
            "Notes",
            adminId,
            DateTimeOffset.UtcNow,
            effectiveDate);

        var assignment = @event.AppointChiefLeader(leaderId, adminId, DateTimeOffset.UtcNow);
        assignment.AcceptDirect(leaderId, DateTimeOffset.UtcNow, hasActiveTeamAssignmentOnCurrentCycle: false);
        @event.Publish(adminId, DateTimeOffset.UtcNow);
        return @event;
    }
}
