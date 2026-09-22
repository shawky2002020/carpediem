using System.Security.Cryptography;

namespace Carpediem.Modules.Events.Application.ChiefLeaderAppointments;

public sealed record AppointedChiefLeader(ChiefLeaderAppointmentView Appointment, bool IsReplay);

public sealed class AppointChiefLeaderHandler(IChiefLeaderAppointmentStore store)
{
    public async Task<AppointedChiefLeader> HandleAsync(
        Guid eventId,
        Guid candidateUserId,
        Guid actorUserId,
        Guid requestToken,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var payload = new byte[32];
        eventId.TryWriteBytes(payload.AsSpan(0, 16));
        candidateUserId.TryWriteBytes(payload.AsSpan(16, 16));
        var fingerprint = SHA256.HashData(payload);
        var stored = await store.AppointAsync(eventId, candidateUserId, actorUserId,
            requestToken, fingerprint, correlationId, DateTimeOffset.UtcNow, cancellationToken);
        return new AppointedChiefLeader(stored.Appointment, stored.IsReplay);
    }
}
