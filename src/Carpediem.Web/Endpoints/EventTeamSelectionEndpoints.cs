using System.Security.Claims;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Application.TeamSelection;
using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Carpediem.Web.Errors;
using Carpediem.Web.Observability;
using Microsoft.AspNetCore.Antiforgery;

namespace Carpediem.Web.Endpoints;

public static class EventTeamSelectionEndpoints
{
    public static IEndpointRouteBuilder MapEventTeamSelectionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events/{eventId}/team-selection")
            .WithTags("Team Selection")
            .RequireAuthorization(AuthorizationPolicies.RequireChiefLeaderOrAdmin);

        group.MapGet("/", GetTeamSelectionContextAsync)
            .WithName("GetTeamSelectionContext")
            .Produces<TeamSelectionContextResponse>(200)
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json");

        group.MapPost("/", SelectVolunteerAsync)
            .WithName("SelectVolunteer")
            .Accepts<SelectVolunteerRequest>("application/json")
            .Produces<TeamAssignmentResponse>(200)
            .Produces<TeamAssignmentResponse>(201)
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());

        return endpoints;
    }

    private static async Task<IResult> GetTeamSelectionContextAsync(
        string eventId,
        HttpContext context,
        ITeamSelectionStore store,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(eventId, out var parsedEventId))
        {
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
        }

        if (!TryGetActor(context, out var actorUserId))
        {
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        try
        {
            var result = await store.GetTeamSelectionContextAsync(parsedEventId, actorUserId, cancellationToken);
            if (result is null)
            {
                return ApiProblemDetailsService.Problem(context, 404, "event_not_found", "The specified event was not found.");
            }

            return Results.Ok(new TeamSelectionContextResponse(
                EventId: result.EventId,
                StaffingCycleId: result.StaffingCycleId,
                EventTitle: result.EventTitle,
                StartDateTime: result.StartDateTime,
                EndDateTime: result.EndDateTime,
                Location: result.Location,
                IsWindowOpen: result.IsWindowOpen,
                AvailableCandidates: result.AvailableCandidates,
                ActiveTeam: result.ActiveTeam));
        }
        catch (InvalidEventStatusException ex)
        {
            return ApiProblemDetailsService.Problem(context, 409, "invalid_event_status",
                $"Team selection is permitted only on Published events. Event {ex.EventId} is currently in {ex.CurrentStatus} status.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return ApiProblemDetailsService.Problem(context, 403, "unauthorized_chief_leader", ex.Message);
        }
    }

    private static async Task<IResult> SelectVolunteerAsync(
        string eventId,
        SelectVolunteerRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        ITeamSelectionStore store,
        CancellationToken cancellationToken)
    {
        using var activity = TeamSelectionTelemetry.Start();

        if (!Guid.TryParse(eventId, out var parsedEventId))
        {
            TeamSelectionTelemetry.Record(activity, "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
        }

        if (request is null || request.CandidateUserId == Guid.Empty)
        {
            TeamSelectionTelemetry.Record(activity, "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Candidate User identifier is required.");
        }

        if (!TryGetActor(context, out var actorUserId))
        {
            TeamSelectionTelemetry.Record(activity, "unauthenticated");
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        var isAdmin = context.User.IsInRole(CarpediemRoles.Admin);

        try
        {
            var result = await store.SelectVolunteerAsync(parsedEventId, request.CandidateUserId, actorUserId, isAdmin, cancellationToken);

            TeamSelectionTelemetry.Record(activity, result.IsIdempotentReplay ? "replay" : "selected");

            var responseBody = new TeamAssignmentResponse(
                Id: result.Id,
                EventId: result.EventId,
                StaffingCycleId: result.StaffingCycleId,
                UserId: result.UserId,
                Status: result.Status,
                AssignedAt: result.AssignedAt,
                AssignedByUserId: result.AssignedByUserId,
                Version: result.Version);

            if (result.IsIdempotentReplay)
            {
                context.Response.Headers.Append("Idempotent-Replay", "true");
                return Results.Ok(responseBody);
            }

            return Results.Created($"/api/events/{eventId}/team-selection", responseBody);
        }
        catch (KeyNotFoundException)
        {
            TeamSelectionTelemetry.Record(activity, "event_not_found");
            return ApiProblemDetailsService.Problem(context, 404, "event_not_found", "The specified event was not found.");
        }
        catch (InvalidEventStatusException ex)
        {
            TeamSelectionTelemetry.Record(activity, "invalid_event_status");
            return ApiProblemDetailsService.Problem(context, 409, "invalid_event_status",
                $"Team selection is permitted only on Published events. Event {ex.EventId} is currently in {ex.CurrentStatus} status.");
        }
        catch (TeamSelectionWindowClosedException)
        {
            TeamSelectionTelemetry.Record(activity, "window_closed");
            return ApiProblemDetailsService.Problem(context, 409, "window_closed",
                "Team selection has closed because the event has already started.");
        }
        catch (ChiefLeaderExclusivityException ex)
        {
            TeamSelectionTelemetry.Record(activity, "chief_leader_conflict");
            return ApiProblemDetailsService.Problem(context, 409, "chief_leader_conflict",
                $"User {ex.UserId} is the active Chief Leader for event {ex.EventId} and cannot be selected as a volunteer team member on that same event.");
        }
        catch (CandidateNotAvailableException ex)
        {
            TeamSelectionTelemetry.Record(activity, "candidate_not_available");
            return ApiProblemDetailsService.Problem(context, 409, "candidate_not_available",
                $"Candidate {ex.CandidateUserId} has not declared availability as Available for event {ex.EventId}.");
        }
        catch (UnauthorizedAccessException ex)
        {
            TeamSelectionTelemetry.Record(activity, "unauthorized_chief_leader");
            return ApiProblemDetailsService.Problem(context, 403, "unauthorized_chief_leader", ex.Message);
        }
    }

    private static bool TryGetActor(HttpContext context, out Guid actorUserId)
    {
        var rawId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawId, out actorUserId);
    }
}

public sealed record TeamSelectionContextResponse(
    Guid EventId,
    Guid StaffingCycleId,
    string EventTitle,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    bool IsWindowOpen,
    IReadOnlyList<AvailableCandidateDto> AvailableCandidates,
    IReadOnlyList<ActiveTeamMemberDto> ActiveTeam);

public sealed record SelectVolunteerRequest(Guid CandidateUserId);

public sealed record TeamAssignmentResponse(
    Guid Id,
    Guid EventId,
    Guid StaffingCycleId,
    Guid UserId,
    string Status,
    DateTimeOffset AssignedAt,
    Guid AssignedByUserId,
    string Version);
