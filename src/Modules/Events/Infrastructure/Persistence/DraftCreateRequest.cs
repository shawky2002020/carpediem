namespace Carpediem.Modules.Events.Infrastructure.Persistence;

public sealed class DraftCreateRequest
{
    private DraftCreateRequest() { }

    public Guid ActorUserId { get; private set; }
    public Guid RequestToken { get; private set; }
    public byte[] PayloadFingerprint { get; private set; } = [];
    public Guid EventId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static DraftCreateRequest Create(Guid actorUserId, Guid requestToken, byte[] fingerprint, Guid eventId, DateTimeOffset createdAt)
    {
        if (fingerprint.Length != 32) throw new ArgumentException("SHA-256 fingerprint must be 32 bytes.", nameof(fingerprint));
        return new DraftCreateRequest
        {
            ActorUserId = actorUserId,
            RequestToken = requestToken,
            PayloadFingerprint = fingerprint.ToArray(),
            EventId = eventId,
            CreatedAt = createdAt,
        };
    }
}
