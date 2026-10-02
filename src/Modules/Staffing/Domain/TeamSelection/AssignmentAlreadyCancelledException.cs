namespace Carpediem.Modules.Staffing.Domain.TeamSelection;

public sealed class AssignmentAlreadyCancelledException(Guid assignmentId)
    : InvalidOperationException($"Team assignment '{assignmentId}' is already cancelled.")
{
    public Guid AssignmentId { get; } = assignmentId;
}
