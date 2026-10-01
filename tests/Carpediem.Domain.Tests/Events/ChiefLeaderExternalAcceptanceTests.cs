using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;

namespace Carpediem.Domain.Tests.Events;

public sealed class ChiefLeaderExternalAcceptanceTests
{
    [Fact]
    public void ExternalAcceptanceActivatesExistingAssignmentWithAdminProvenance()
    {
        var (draft, assignment) = Pending();
        var adminId = Guid.NewGuid();
        var acceptedAt = new DateTimeOffset(2026, 9, 23, 7, 30, 0, TimeSpan.Zero);

        assignment.AcceptExternal(adminId, acceptedAt, hasActiveTeamAssignmentOnCurrentCycle: false);

        Assert.Equal(ChiefLeaderAssignmentStatus.Active, assignment.Status);
        Assert.Equal(adminId, assignment.AcceptedByUserId);
        Assert.Equal(acceptedAt, assignment.AcceptedAt);
        Assert.Equal("AdminExternal", assignment.AcceptanceSource);
        Assert.Null(assignment.DeclinedAt);
        Assert.Equal(EventStatus.Draft, draft.Status);
    }

    [Fact]
    public void ExternalAcceptanceRejectsTeamConflictAndLeavesPending()
    {
        var (_, assignment) = Pending();

        Assert.Throws<ActiveTeamAssignmentConflictException>(() =>
            assignment.AcceptExternal(Guid.NewGuid(), DateTimeOffset.UtcNow,
                hasActiveTeamAssignmentOnCurrentCycle: true));
        Assert.Equal(ChiefLeaderAssignmentStatus.Pending, assignment.Status);
    }

    [Fact]
    public void ExternalAcceptanceRejectsMissingAdminNonUtcAndNonPending()
    {
        var (_, assignment) = Pending();
        Assert.Throws<ArgumentException>(() =>
            assignment.AcceptExternal(Guid.Empty, DateTimeOffset.UtcNow, false));
        Assert.Throws<ArgumentException>(() =>
            assignment.AcceptExternal(Guid.NewGuid(),
                new DateTimeOffset(2026, 9, 23, 10, 30, 0, TimeSpan.FromHours(3)), false));

        assignment.AcceptExternal(Guid.NewGuid(), DateTimeOffset.UtcNow, false);
        Assert.Throws<ChiefLeaderResponseStateException>(() =>
            assignment.AcceptExternal(Guid.NewGuid(), DateTimeOffset.UtcNow, false));
    }

    private static (Event Draft, ChiefLeaderAssignment Assignment) Pending()
    {
        var start = new DateTimeOffset(2026, 10, 10, 11, 30, 0, TimeSpan.Zero);
        var draft = Event.CreateDraft("Beach cleanup", start, start.AddHours(2), "North shore", null,
            Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2026, 10, 10));
        var assignment = draft.AppointChiefLeader(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow);
        return (draft, assignment);
    }
}
