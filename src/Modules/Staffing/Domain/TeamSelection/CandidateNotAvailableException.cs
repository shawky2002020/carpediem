namespace Carpediem.Modules.Staffing.Domain.TeamSelection;

public sealed class CandidateNotAvailableException(Guid eventId, Guid candidateUserId)
    : InvalidOperationException($"Candidate '{candidateUserId}' has not declared availability as Available for event '{eventId}'.")
{
    public Guid EventId { get; } = eventId;
    public Guid CandidateUserId { get; } = candidateUserId;
}
