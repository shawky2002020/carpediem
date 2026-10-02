using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Modules.Staffing.Domain.TeamSelection;

namespace Carpediem.Domain.Tests.Staffing;

public sealed class TeamAssignmentTests
{
    private readonly Guid _staffingCycleId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _candidateUserId = Guid.NewGuid();
    private readonly Guid _assignedByUserId = Guid.NewGuid();
    private readonly DateTimeOffset _futureEventStart = DateTimeOffset.UtcNow.AddDays(7);
    private readonly DateTimeOffset _assignedAt = DateTimeOffset.UtcNow;

    [Fact]
    public void CreateWithValidParametersCreatesActiveTeamAssignment()
    {
        var assignment = TeamAssignment.Create(
            _staffingCycleId,
            _eventId,
            _candidateUserId,
            _assignedByUserId,
            _futureEventStart,
            _assignedAt,
            candidateAvailabilityResponse: "Available",
            isCandidateChiefLeaderOnEvent: false,
            eventStatus: "Published");

        Assert.NotEqual(Guid.Empty, assignment.Id);
        Assert.Equal(_staffingCycleId, assignment.StaffingCycleId);
        Assert.Equal(_eventId, assignment.EventId);
        Assert.Equal(_candidateUserId, assignment.UserId);
        Assert.Equal(_assignedByUserId, assignment.AssignedByUserId);
        Assert.Equal(TeamAssignmentStatus.Active, assignment.Status);
        Assert.Equal(_assignedAt, assignment.AssignedAt);
        Assert.Null(assignment.CancellationReason);
        Assert.Null(assignment.CancelledAt);
    }

    [Fact]
    public void CreateWhenEventIsNotPublishedThrowsInvalidEventStatusException()
    {
        var ex = Assert.Throws<InvalidEventStatusException>(() =>
            TeamAssignment.Create(
                _staffingCycleId,
                _eventId,
                _candidateUserId,
                _assignedByUserId,
                _futureEventStart,
                _assignedAt,
                candidateAvailabilityResponse: "Available",
                isCandidateChiefLeaderOnEvent: false,
                eventStatus: "Draft"));

        Assert.Equal(_eventId, ex.EventId);
        Assert.Equal("Draft", ex.CurrentStatus);
    }

    [Fact]
    public void CreateWhenEventStartHasPassedThrowsTeamSelectionWindowClosedException()
    {
        var pastStart = DateTimeOffset.UtcNow.AddHours(-1);
        var ex = Assert.Throws<TeamSelectionWindowClosedException>(() =>
            TeamAssignment.Create(
                _staffingCycleId,
                _eventId,
                _candidateUserId,
                _assignedByUserId,
                pastStart,
                _assignedAt,
                candidateAvailabilityResponse: "Available",
                isCandidateChiefLeaderOnEvent: false,
                eventStatus: "Published"));

        Assert.Equal(_eventId, ex.EventId);
    }

    [Fact]
    public void CreateWhenCandidateIsChiefLeaderOnEventThrowsChiefLeaderExclusivityException()
    {
        var ex = Assert.Throws<ChiefLeaderExclusivityException>(() =>
            TeamAssignment.Create(
                _staffingCycleId,
                _eventId,
                _candidateUserId,
                _assignedByUserId,
                _futureEventStart,
                _assignedAt,
                candidateAvailabilityResponse: "Available",
                isCandidateChiefLeaderOnEvent: true,
                eventStatus: "Published"));

        Assert.Equal(_eventId, ex.EventId);
        Assert.Equal(_candidateUserId, ex.UserId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("NotAvailable")]
    [InlineData("Unknown")]
    public void CreateWhenCandidateIsNotAvailableThrowsCandidateNotAvailableException(string? response)
    {
        var ex = Assert.Throws<CandidateNotAvailableException>(() =>
            TeamAssignment.Create(
                _staffingCycleId,
                _eventId,
                _candidateUserId,
                _assignedByUserId,
                _futureEventStart,
                _assignedAt,
                candidateAvailabilityResponse: response,
                isCandidateChiefLeaderOnEvent: false,
                eventStatus: "Published"));

        Assert.Equal(_eventId, ex.EventId);
        Assert.Equal(_candidateUserId, ex.CandidateUserId);
    }

    [Fact]
    public void CreateWithEmptyIdsOrNonUtcThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            TeamAssignment.Create(
                Guid.Empty,
                _eventId,
                _candidateUserId,
                _assignedByUserId,
                _futureEventStart,
                _assignedAt,
                "Available",
                false,
                "Published"));

        var nonUtc = new DateTimeOffset(2030, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));
        Assert.Throws<ArgumentException>(() =>
            TeamAssignment.Create(
                _staffingCycleId,
                _eventId,
                _candidateUserId,
                _assignedByUserId,
                _futureEventStart,
                nonUtc,
                "Available",
                false,
                "Published"));
    }

    [Fact]
    public void CreateDirectAssignmentWithNonRespondentCreatesActiveTeamAssignment()
    {
        var assignment = TeamAssignment.CreateDirectAssignment(
            _staffingCycleId,
            _eventId,
            _candidateUserId,
            _assignedByUserId,
            _futureEventStart,
            _assignedAt,
            isCandidateChiefLeaderOnEvent: false,
            eventStatus: "Published");

        Assert.NotEqual(Guid.Empty, assignment.Id);
        Assert.Equal(_staffingCycleId, assignment.StaffingCycleId);
        Assert.Equal(_eventId, assignment.EventId);
        Assert.Equal(_candidateUserId, assignment.UserId);
        Assert.Equal(_assignedByUserId, assignment.AssignedByUserId);
        Assert.Equal(TeamAssignmentStatus.Active, assignment.Status);
        Assert.Equal(_assignedAt, assignment.AssignedAt);
    }

    [Fact]
    public void CreateDirectAssignmentWhenCandidateIsChiefLeaderThrowsChiefLeaderExclusivityException()
    {
        var ex = Assert.Throws<ChiefLeaderExclusivityException>(() =>
            TeamAssignment.CreateDirectAssignment(
                _staffingCycleId,
                _eventId,
                _candidateUserId,
                _assignedByUserId,
                _futureEventStart,
                _assignedAt,
                isCandidateChiefLeaderOnEvent: true,
                eventStatus: "Published"));

        Assert.Equal(_eventId, ex.EventId);
        Assert.Equal(_candidateUserId, ex.UserId);
    }

    [Fact]
    public void CreateDirectAssignmentWhenWindowClosedThrowsTeamSelectionWindowClosedException()
    {
        var pastStart = DateTimeOffset.UtcNow.AddHours(-1);
        var ex = Assert.Throws<TeamSelectionWindowClosedException>(() =>
            TeamAssignment.CreateDirectAssignment(
                _staffingCycleId,
                _eventId,
                _candidateUserId,
                _assignedByUserId,
                pastStart,
                _assignedAt,
                isCandidateChiefLeaderOnEvent: false,
                eventStatus: "Published"));

        Assert.Equal(_eventId, ex.EventId);
    }

    [Fact]
    public void CreateDirectAssignmentWhenEventNotPublishedThrowsInvalidEventStatusException()
    {
        var ex = Assert.Throws<InvalidEventStatusException>(() =>
            TeamAssignment.CreateDirectAssignment(
                _staffingCycleId,
                _eventId,
                _candidateUserId,
                _assignedByUserId,
                _futureEventStart,
                _assignedAt,
                isCandidateChiefLeaderOnEvent: false,
                eventStatus: "Draft"));

        Assert.Equal(_eventId, ex.EventId);
        Assert.Equal("Draft", ex.CurrentStatus);
    }
}
