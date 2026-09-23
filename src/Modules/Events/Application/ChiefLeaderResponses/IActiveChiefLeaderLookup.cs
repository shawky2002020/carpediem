namespace Carpediem.Modules.Events.Application.ChiefLeaderResponses;

public interface IActiveChiefLeaderLookup
{
    Task<bool> IsActiveChiefLeaderAsync(Guid eventId, Guid userId,
        CancellationToken cancellationToken);
}
