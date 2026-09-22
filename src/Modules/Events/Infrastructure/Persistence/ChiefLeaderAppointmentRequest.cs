namespace Carpediem.Modules.Events.Infrastructure.Persistence;

public sealed class ChiefLeaderAppointmentRequest
{
    private ChiefLeaderAppointmentRequest() { }

    public Guid ActorUserId { get; private set; }
    public Guid RequestToken { get; private set; }
    public byte[] PayloadFingerprint { get; private set; } = [];
    public Guid AssignmentId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static ChiefLeaderAppointmentRequest Create(Guid actorUserId, Guid requestToken,
        byte[] payloadFingerprint, Guid assignmentId, DateTimeOffset createdAt) => new()
        {
            ActorUserId = actorUserId,
            RequestToken = requestToken,
            PayloadFingerprint = payloadFingerprint,
            AssignmentId = assignmentId,
            CreatedAt = createdAt,
        };
}
