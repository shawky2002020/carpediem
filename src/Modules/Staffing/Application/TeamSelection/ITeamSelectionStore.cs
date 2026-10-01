namespace Carpediem.Modules.Staffing.Application.TeamSelection;

public interface ITeamSelectionStore
{
    Task<TeamSelectionContext?> GetTeamSelectionContextAsync(
        Guid eventId,
        Guid actingUserId,
        CancellationToken cancellationToken = default);

    Task<SelectVolunteerResult> SelectVolunteerAsync(
        Guid eventId,
        Guid candidateUserId,
        Guid actingUserId,
        bool isAdminActor,
        CancellationToken cancellationToken = default);
}

public sealed record TeamSelectionContext(
    Guid EventId,
    Guid StaffingCycleId,
    string EventTitle,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    bool IsWindowOpen,
    IReadOnlyList<AvailableCandidateDto> AvailableCandidates,
    IReadOnlyList<ActiveTeamMemberDto> ActiveTeam);

public sealed record AvailableCandidateDto(
    Guid UserId,
    string UserName,
    DateTimeOffset DeclaredAt);

public sealed record ActiveTeamMemberDto(
    Guid AssignmentId,
    Guid UserId,
    string UserName,
    DateTimeOffset AssignedAt,
    string Version);

public sealed record SelectVolunteerResult(
    Guid Id,
    Guid EventId,
    Guid StaffingCycleId,
    Guid UserId,
    string Status,
    DateTimeOffset AssignedAt,
    Guid AssignedByUserId,
    string Version,
    bool IsIdempotentReplay);
