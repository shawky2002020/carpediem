namespace Carpediem.Modules.Identity.Application;

public sealed record ChiefLeaderCandidate(Guid UserId, string UserName);

public interface IChiefLeaderCandidateDirectory
{
    Task<IReadOnlyList<ChiefLeaderCandidate>> ListEligibleAsync(CancellationToken cancellationToken);
    Task<ChiefLeaderCandidate?> FindEligibleAsync(Guid userId, CancellationToken cancellationToken);
}
