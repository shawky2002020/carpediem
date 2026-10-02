using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Modules.Staffing.Domain.TeamSelection;

namespace Carpediem.Domain.Tests.Staffing;

#pragma warning disable CA1707 // Identifiers should not contain underscores

public sealed class TeamAssignmentReplacementTests
{
    private readonly Guid _staffingCycleId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _candidateUserId = Guid.NewGuid();
    private readonly Guid _chiefLeaderUserId = Guid.NewGuid();
    private readonly DateTimeOffset _nowUtc = DateTimeOffset.UtcNow;

    [Fact]
    public void CreateReplacementAssignment_WhenValidPublishedFutureEvent_CreatesActiveAssignment()
    {
        var eventStartUtc = _nowUtc.AddDays(7);

        var assignment = TeamAssignment.CreateReplacementAssignment(
            _staffingCycleId,
            _eventId,
            _candidateUserId,
            _chiefLeaderUserId,
            eventStartUtc,
            _nowUtc,
            isCandidateChiefLeaderOnEvent: false,
            eventStatus: "Published");

        Assert.NotNull(assignment);
        Assert.NotEqual(Guid.Empty, assignment.Id);
        Assert.Equal(_staffingCycleId, assignment.StaffingCycleId);
        Assert.Equal(_eventId, assignment.EventId);
        Assert.Equal(_candidateUserId, assignment.UserId);
        Assert.Equal(_chiefLeaderUserId, assignment.AssignedByUserId);
        Assert.Equal(_nowUtc, assignment.AssignedAt);
        Assert.Equal(TeamAssignmentStatus.Active, assignment.Status);
        Assert.Null(assignment.CancellationReason);
        Assert.Null(assignment.CancelledAt);
    }

    [Fact]
    public void CreateReplacementAssignment_WhenEventNotPublished_ThrowsInvalidEventStatusException()
    {
        var eventStartUtc = _nowUtc.AddDays(7);

        var ex = Assert.Throws<InvalidEventStatusException>(() =>
            TeamAssignment.CreateReplacementAssignment(
                _staffingCycleId,
                _eventId,
                _candidateUserId,
                _chiefLeaderUserId,
                eventStartUtc,
                _nowUtc,
                isCandidateChiefLeaderOnEvent: false,
                eventStatus: "Draft"));

        Assert.Equal(_eventId, ex.EventId);
        Assert.Equal("Draft", ex.CurrentStatus);
    }

    [Fact]
    public void CreateReplacementAssignment_WhenEventStartPassed_ThrowsTeamSelectionWindowClosedException()
    {
        var eventStartUtc = _nowUtc.AddMinutes(-5);

        var ex = Assert.Throws<TeamSelectionWindowClosedException>(() =>
            TeamAssignment.CreateReplacementAssignment(
                _staffingCycleId,
                _eventId,
                _candidateUserId,
                _chiefLeaderUserId,
                eventStartUtc,
                _nowUtc,
                isCandidateChiefLeaderOnEvent: false,
                eventStatus: "Published"));

        Assert.Equal(_eventId, ex.EventId);
    }

    [Fact]
    public void CreateReplacementAssignment_WhenCandidateIsChiefLeader_ThrowsChiefLeaderExclusivityException()
    {
        var eventStartUtc = _nowUtc.AddDays(7);

        var ex = Assert.Throws<ChiefLeaderExclusivityException>(() =>
            TeamAssignment.CreateReplacementAssignment(
                _staffingCycleId,
                _eventId,
                _chiefLeaderUserId,
                _chiefLeaderUserId,
                eventStartUtc,
                _nowUtc,
                isCandidateChiefLeaderOnEvent: true,
                eventStatus: "Published"));

        Assert.Equal(_eventId, ex.EventId);
        Assert.Equal(_chiefLeaderUserId, ex.UserId);
    }

    [Fact]
    public void CreateReplacementAssignment_WhenCandidateUserIdEmpty_ThrowsArgumentException()
    {
        var eventStartUtc = _nowUtc.AddDays(7);

        Assert.Throws<ArgumentException>(() =>
            TeamAssignment.CreateReplacementAssignment(
                _staffingCycleId,
                _eventId,
                Guid.Empty,
                _chiefLeaderUserId,
                eventStartUtc,
                _nowUtc,
                isCandidateChiefLeaderOnEvent: false,
                eventStatus: "Published"));
    }
}
