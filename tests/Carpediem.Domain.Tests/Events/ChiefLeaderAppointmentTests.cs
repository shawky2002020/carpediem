using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;

namespace Carpediem.Domain.Tests.Events;

public sealed class ChiefLeaderAppointmentTests
{
    [Fact]
    public void AppointsOnePendingChiefLeaderWithoutChangingDraft()
    {
        var draft = CreateDraft();
        var candidate = Guid.NewGuid();
        var admin = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 22, 9, 0, 0, TimeSpan.Zero);

        var assignment = draft.AppointChiefLeader(candidate, admin, now);

        Assert.Equal(EventStatus.Draft, draft.Status);
        Assert.Equal(ChiefLeaderAssignmentStatus.Pending, assignment.Status);
        Assert.Equal(draft.Id, assignment.EventId);
        Assert.Equal(candidate, assignment.ChiefLeaderUserId);
        Assert.Equal(admin, assignment.AppointedByUserId);
        Assert.Equal(now, assignment.AppointedAt);
        Assert.Null(assignment.AcceptedAt);
        Assert.Null(assignment.AcceptanceSource);
        Assert.Null(assignment.AcceptedByUserId);
        Assert.Null(assignment.DeclinedAt);
        Assert.Same(assignment, Assert.Single(draft.ChiefLeaderAssignments));
    }

    [Fact]
    public void RejectsSecondUnresolvedAppointment()
    {
        var draft = CreateDraft();
        draft.AppointChiefLeader(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<ChiefLeaderAlreadyAssignedException>(() =>
            draft.AppointChiefLeader(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow));
        Assert.Single(draft.ChiefLeaderAssignments);
    }

    [Fact]
    public void RequiresUtcAppointmentTime()
    {
        var draft = CreateDraft();
        Assert.Throws<ArgumentException>(() => draft.AppointChiefLeader(Guid.NewGuid(), Guid.NewGuid(),
            new DateTimeOffset(2026, 9, 22, 12, 0, 0, TimeSpan.FromHours(3))));
        Assert.Empty(draft.ChiefLeaderAssignments);
    }

    private static Event CreateDraft()
    {
        var start = new DateTimeOffset(2026, 10, 10, 11, 30, 0, TimeSpan.Zero);
        return Event.CreateDraft("Beach cleanup", start, start.AddHours(2), "North shore", null,
            Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2026, 10, 10));
    }
}
