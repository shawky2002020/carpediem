using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;

namespace Carpediem.Domain.Tests.Events;

public sealed class EventPublicationTests
{
    [Fact]
    public void PublishTransitionsDraftToPublishedWhenActiveChiefLeaderExists()
    {
        var draft = CreateDraftWithActiveChiefLeader(out _);
        var adminId = Guid.NewGuid();
        var publishedAt = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

        draft.Publish(adminId, publishedAt);

        Assert.Equal(EventStatus.Published, draft.Status);
    }

    [Fact]
    public void PublishThrowsWhenEventIsNotDraft()
    {
        var draft = CreateDraftWithActiveChiefLeader(out _);
        var adminId = Guid.NewGuid();
        var publishedAt = new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

        draft.Publish(adminId, publishedAt);

        Assert.Throws<NotDraftException>(() => draft.Publish(adminId, publishedAt));
    }

    [Fact]
    public void PublishThrowsWhenNoChiefLeaderAppointed()
    {
        var start = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var draft = Event.CreateDraft("Gala Event", start, start.AddHours(4), "Main Hall", null,
            Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2026, 11, 1));

        Assert.Throws<ActiveChiefLeaderRequiredException>(() =>
            draft.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PublishThrowsWhenChiefLeaderIsPendingOrDeclined()
    {
        var start = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var draft = Event.CreateDraft("Gala Event", start, start.AddHours(4), "Main Hall", null,
            Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2026, 11, 1));
        var candidateId = Guid.NewGuid();
        var assignment = draft.AppointChiefLeader(candidateId, Guid.NewGuid(), DateTimeOffset.UtcNow);

        // Pending
        Assert.Throws<ActiveChiefLeaderRequiredException>(() =>
            draft.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow));

        // Declined
        assignment.DeclineDirect(candidateId, DateTimeOffset.UtcNow);
        Assert.Throws<ActiveChiefLeaderRequiredException>(() =>
            draft.Publish(Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void PublishThrowsWhenTimeOrAdminArgumentsAreInvalid()
    {
        var draft = CreateDraftWithActiveChiefLeader(out _);

        Assert.Throws<ArgumentException>(() =>
            draft.Publish(Guid.Empty, DateTimeOffset.UtcNow));

        Assert.Throws<ArgumentException>(() =>
            draft.Publish(Guid.NewGuid(), new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.FromHours(2))));
    }

    private static Event CreateDraftWithActiveChiefLeader(out ChiefLeaderAssignment assignment)
    {
        var start = new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
        var draft = Event.CreateDraft("Gala Event", start, start.AddHours(4), "Main Hall", null,
            Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2026, 11, 1));
        var candidateId = Guid.NewGuid();
        assignment = draft.AppointChiefLeader(candidateId, Guid.NewGuid(), DateTimeOffset.UtcNow);
        assignment.AcceptDirect(candidateId, DateTimeOffset.UtcNow, hasActiveTeamAssignmentOnCurrentCycle: false);
        return draft;
    }
}
