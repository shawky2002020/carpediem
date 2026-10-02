using Carpediem.Modules.Staffing.Domain.TeamSelection;

namespace Carpediem.Domain.Tests.Staffing;

public sealed class TeamAssignmentCancelTests
{
    private readonly Guid _staffingCycleId = Guid.NewGuid();
    private readonly Guid _eventId = Guid.NewGuid();
    private readonly Guid _candidateUserId = Guid.NewGuid();
    private readonly Guid _assignedByUserId = Guid.NewGuid();
    private readonly DateTimeOffset _futureEventStart = DateTimeOffset.UtcNow.AddDays(7);
    private readonly DateTimeOffset _assignedAt = DateTimeOffset.UtcNow.AddHours(-1);

    private TeamAssignment CreateActiveAssignment()
    {
        return TeamAssignment.Create(
            _staffingCycleId,
            _eventId,
            _candidateUserId,
            _assignedByUserId,
            _futureEventStart,
            _assignedAt,
            candidateAvailabilityResponse: "Available",
            isCandidateChiefLeaderOnEvent: false,
            eventStatus: "Published");
    }

    [Fact]
    public void CancelWithValidJustificationAndFutureEventTransitionsToCancelled()
    {
        var assignment = CreateActiveAssignment();
        var cancelledAt = DateTimeOffset.UtcNow;
        const string justification = "  Unavoidable family emergency conflict.  ";

        assignment.Cancel(justification, cancelledAt, _futureEventStart);

        Assert.Equal(TeamAssignmentStatus.Cancelled, assignment.Status);
        Assert.Equal("Unavoidable family emergency conflict.", assignment.CancellationReason);
        Assert.Equal(cancelledAt, assignment.CancelledAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CancelWhenJustificationIsNullOrWhitespaceThrowsArgumentException(string? invalidJustification)
    {
        var assignment = CreateActiveAssignment();
        var cancelledAt = DateTimeOffset.UtcNow;

        var ex = Assert.Throws<ArgumentException>(() =>
            assignment.Cancel(invalidJustification!, cancelledAt, _futureEventStart));

        Assert.Equal("justification", ex.ParamName);
    }

    [Fact]
    public void CancelWhenJustificationExceeds500CharactersThrowsArgumentException()
    {
        var assignment = CreateActiveAssignment();
        var cancelledAt = DateTimeOffset.UtcNow;
        var excessiveJustification = new string('a', 501);

        var ex = Assert.Throws<ArgumentException>(() =>
            assignment.Cancel(excessiveJustification, cancelledAt, _futureEventStart));

        Assert.Equal("justification", ex.ParamName);
    }

    [Fact]
    public void CancelWhenAlreadyCancelledThrowsAssignmentAlreadyCancelledException()
    {
        var assignment = CreateActiveAssignment();
        var cancelledAt = DateTimeOffset.UtcNow;

        assignment.Cancel("First cancellation reason", cancelledAt, _futureEventStart);

        var ex = Assert.Throws<AssignmentAlreadyCancelledException>(() =>
            assignment.Cancel("Second cancellation reason", cancelledAt.AddMinutes(5), _futureEventStart));

        Assert.Equal(assignment.Id, ex.AssignmentId);
    }

    [Fact]
    public void CancelWhenEventStartHasPassedThrowsCancellationWindowClosedException()
    {
        var assignment = CreateActiveAssignment();
        var pastEventStart = DateTimeOffset.UtcNow.AddHours(-2);
        var cancelledAt = DateTimeOffset.UtcNow;

        var ex = Assert.Throws<CancellationWindowClosedException>(() =>
            assignment.Cancel("Late cancellation attempt", cancelledAt, pastEventStart));

        Assert.Equal(_eventId, ex.EventId);
    }

    [Fact]
    public void CancelWhenCancelledAtNotUtcThrowsArgumentException()
    {
        var assignment = CreateActiveAssignment();
        var nonUtcCancelledAt = new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.FromHours(3));

        var ex = Assert.Throws<ArgumentException>(() =>
            assignment.Cancel("Valid reason", nonUtcCancelledAt, _futureEventStart));

        Assert.Equal("cancelledAtUtc", ex.ParamName);
    }

    [Fact]
    public void CancelWhenEventStartNotUtcThrowsArgumentException()
    {
        var assignment = CreateActiveAssignment();
        var cancelledAt = DateTimeOffset.UtcNow;
        var nonUtcEventStart = new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.FromHours(3));

        var ex = Assert.Throws<ArgumentException>(() =>
            assignment.Cancel("Valid reason", cancelledAt, nonUtcEventStart));

        Assert.Equal("eventStartUtc", ex.ParamName);
    }
}
