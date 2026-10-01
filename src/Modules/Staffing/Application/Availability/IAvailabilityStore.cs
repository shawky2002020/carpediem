using Carpediem.Modules.Staffing.Domain.Availability;

namespace Carpediem.Modules.Staffing.Application.Availability;

public record DeclareAvailabilityCommand(
    Guid EventId,
    Guid UserId,
    AvailabilityResponseType Response,
    byte[]? RowVersion,
    Guid CorrelationId);

public record MyAvailabilityDto(
    Guid EventId,
    Guid StaffingCycleId,
    AvailabilityResponseType? Response,
    DateTimeOffset? DeclaredAt,
    string? Version,
    bool IsWindowOpen,
    string EventStatus,
    bool IsChiefLeaderOnEvent);

public record AvailabilityDeclarationResult(
    Guid Id,
    Guid EventId,
    Guid StaffingCycleId,
    Guid UserId,
    AvailabilityResponseType Response,
    DateTimeOffset DeclaredAt,
    string Version,
    bool IsIdempotentReplay,
    bool IsNew);

public interface IAvailabilityStore
{
    Task<MyAvailabilityDto?> GetMyAvailabilityAsync(Guid eventId, Guid userId, CancellationToken cancellationToken);
    Task<AvailabilityDeclarationResult> DeclareAvailabilityAsync(DeclareAvailabilityCommand command, CancellationToken cancellationToken);
}
