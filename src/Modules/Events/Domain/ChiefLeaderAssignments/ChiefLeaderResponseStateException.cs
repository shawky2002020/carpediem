namespace Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;

public sealed class ChiefLeaderResponseStateException : Exception
{
    public ChiefLeaderResponseStateException() : base("The appointment is no longer Pending.") { }
}

public sealed class ActiveTeamAssignmentConflictException : Exception
{
    public ActiveTeamAssignmentConflictException() : base("The candidate already has an Active team assignment on this Event.") { }
}
