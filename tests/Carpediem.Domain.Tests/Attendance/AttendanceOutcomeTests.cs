using Carpediem.Modules.Attendance.Domain;

namespace Carpediem.Domain.Tests.Attendance;

public sealed class AttendanceOutcomeTests
{
    private readonly Guid _teamAssignmentId = Guid.NewGuid();
    private readonly Guid _chiefLeaderUserId = Guid.NewGuid();
    private readonly DateTimeOffset _finalizedAt = DateTimeOffset.UtcNow;

    [Fact]
    public void CreateWithValidParametersCreatesOutcome()
    {
        var outcome = AttendanceOutcome.Create(
            _teamAssignmentId,
            AttendanceOutcomeValue.Attended,
            _chiefLeaderUserId,
            _finalizedAt);

        Assert.NotEqual(Guid.Empty, outcome.Id);
        Assert.Equal(_teamAssignmentId, outcome.TeamAssignmentId);
        Assert.Equal(AttendanceOutcomeValue.Attended, outcome.Outcome);
        Assert.Equal(_chiefLeaderUserId, outcome.FinalizedByUserId);
        Assert.Equal(_finalizedAt, outcome.FinalizedAt);
        Assert.Null(outcome.CorrectedByUserId);
        Assert.Null(outcome.CorrectedAt);
    }

    [Fact]
    public void CreateWithEmptyTeamAssignmentIdThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            AttendanceOutcome.Create(
                Guid.Empty,
                AttendanceOutcomeValue.Attended,
                _chiefLeaderUserId,
                _finalizedAt));

        Assert.Equal("teamAssignmentId", ex.ParamName);
    }

    [Fact]
    public void CreateWithEmptyFinalizedByUserIdThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            AttendanceOutcome.Create(
                _teamAssignmentId,
                AttendanceOutcomeValue.NoShow,
                Guid.Empty,
                _finalizedAt));

        Assert.Equal("finalizedByUserId", ex.ParamName);
    }

    [Fact]
    public void CreateWithInvalidOutcomeValueThrowsArgumentOutOfRangeException()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() =>
            AttendanceOutcome.Create(
                _teamAssignmentId,
                (AttendanceOutcomeValue)999,
                _chiefLeaderUserId,
                _finalizedAt));

        Assert.Equal("outcome", ex.ParamName);
    }

    [Fact]
    public void UpdateOutcomeUpdatesOutcomeAndTimestamp()
    {
        var outcome = AttendanceOutcome.Create(
            _teamAssignmentId,
            AttendanceOutcomeValue.Attended,
            _chiefLeaderUserId,
            _finalizedAt);

        var newChiefLeader = Guid.NewGuid();
        var updatedAt = _finalizedAt.AddMinutes(15);

        outcome.UpdateOutcome(AttendanceOutcomeValue.NoShow, newChiefLeader, updatedAt);

        Assert.Equal(AttendanceOutcomeValue.NoShow, outcome.Outcome);
        Assert.Equal(newChiefLeader, outcome.FinalizedByUserId);
        Assert.Equal(updatedAt, outcome.FinalizedAt);
    }

    [Fact]
    public void UpdateOutcomeWithInvalidOutcomeValueThrowsArgumentOutOfRangeException()
    {
        var outcome = AttendanceOutcome.Create(
            _teamAssignmentId,
            AttendanceOutcomeValue.Attended,
            _chiefLeaderUserId,
            _finalizedAt);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            outcome.UpdateOutcome((AttendanceOutcomeValue)888, _chiefLeaderUserId, DateTimeOffset.UtcNow));
    }
}
