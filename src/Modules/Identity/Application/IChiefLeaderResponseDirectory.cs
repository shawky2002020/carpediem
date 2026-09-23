namespace Carpediem.Modules.Identity.Application;

public interface IChiefLeaderResponseDirectory
{
    Task<bool> IsActiveChiefLeaderAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> ListActiveAdminIdsAsync(CancellationToken cancellationToken);
}
