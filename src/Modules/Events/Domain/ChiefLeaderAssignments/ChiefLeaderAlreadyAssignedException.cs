namespace Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;

public sealed class ChiefLeaderAlreadyAssignedException : Exception
{
    public ChiefLeaderAlreadyAssignedException() : base("The Event already has an unresolved Chief Leader assignment.") { }
}
