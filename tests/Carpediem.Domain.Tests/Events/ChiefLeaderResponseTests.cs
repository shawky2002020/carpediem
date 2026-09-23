using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;

namespace Carpediem.Domain.Tests.Events;

public sealed class ChiefLeaderResponseTests
{
    [Fact]
    public void DirectAcceptActivatesExistingPendingAssignmentAndLeavesDraft()
    {
        var (draft, assignment, candidate) = Pending();
        var respondedAt = new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);

        assignment.AcceptDirect(candidate, respondedAt, hasActiveTeamAssignmentOnCurrentCycle: false);

        Assert.Equal(ChiefLeaderAssignmentStatus.Active, assignment.Status);
        Assert.Equal(candidate, assignment.AcceptedByUserId);
        Assert.Equal(respondedAt, assignment.AcceptedAt);
        Assert.Equal("Direct", assignment.AcceptanceSource);
        Assert.Null(assignment.DeclinedAt);
        Assert.Equal(EventStatus.Draft, draft.Status);
        Assert.Single(draft.ChiefLeaderAssignments);
    }

    [Fact]
    public void SameEventActiveTeamAssignmentBlocksActivationButNotDecline()
    {
        var (_, assignment, candidate) = Pending();

        Assert.Throws<ActiveTeamAssignmentConflictException>(() =>
            assignment.AcceptDirect(candidate, DateTimeOffset.UtcNow, hasActiveTeamAssignmentOnCurrentCycle: true));
        Assert.Equal(ChiefLeaderAssignmentStatus.Pending, assignment.Status);

        assignment.DeclineDirect(candidate, DateTimeOffset.UtcNow);
        Assert.Equal(ChiefLeaderAssignmentStatus.Declined, assignment.Status);
    }

    [Fact]
    public void DirectDeclinePreservesAssignmentWithoutAcceptanceFields()
    {
        var (draft, assignment, candidate) = Pending();
        var respondedAt = new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);

        assignment.DeclineDirect(candidate, respondedAt);

        Assert.Equal(ChiefLeaderAssignmentStatus.Declined, assignment.Status);
        Assert.Equal(respondedAt, assignment.DeclinedAt);
        Assert.Null(assignment.AcceptedAt);
        Assert.Null(assignment.AcceptedByUserId);
        Assert.Null(assignment.AcceptanceSource);
        Assert.Equal(EventStatus.Draft, draft.Status);
        Assert.Single(draft.ChiefLeaderAssignments);
    }

    [Fact]
    public void RejectsWrongCandidateNonPendingAndNonUtcResponse()
    {
        var (_, assignment, candidate) = Pending();
        Assert.Throws<UnauthorizedAccessException>(() =>
            assignment.AcceptDirect(Guid.NewGuid(), DateTimeOffset.UtcNow, false));
        Assert.Throws<ArgumentException>(() =>
            assignment.DeclineDirect(candidate, new DateTimeOffset(2026, 9, 22, 13, 0, 0, TimeSpan.FromHours(3))));

        assignment.AcceptDirect(candidate, DateTimeOffset.UtcNow, false);
        Assert.Throws<ChiefLeaderResponseStateException>(() =>
            assignment.DeclineDirect(candidate, DateTimeOffset.UtcNow));
        Assert.Throws<ChiefLeaderResponseStateException>(() =>
            assignment.AcceptDirect(candidate, DateTimeOffset.UtcNow, false));
    }

    private static (Event Draft, ChiefLeaderAssignment Assignment, Guid Candidate) Pending()
    {
        var start = new DateTimeOffset(2026, 10, 10, 11, 30, 0, TimeSpan.Zero);
        var draft = Event.CreateDraft("Beach cleanup", start, start.AddHours(2), "North shore", null,
            Guid.NewGuid(), DateTimeOffset.UtcNow, new DateOnly(2026, 10, 10));
        var candidate = Guid.NewGuid();
        return (draft, draft.AppointChiefLeader(candidate, Guid.NewGuid(), DateTimeOffset.UtcNow), candidate);
    }
}
