using System.Security.Claims;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Application.DirectAssignment;
using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Carpediem.Web.Errors;
using Carpediem.Web.Observability;
using Microsoft.AspNetCore.Antiforgery;

namespace Carpediem.Web.Endpoints;

public static class EventDirectAssignmentEndpoints
{
    public static IEndpointRouteBuilder MapEventDirectAssignmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events/{eventId}/direct-assignment")
            .WithTags("Direct Assignment")
            .RequireAuthorization(AuthorizationPolicies.RequireChiefLeaderOrAdmin);

        group.MapGet("/candidates", GetCandidatesAsync)
            .WithName("GetDirectAssignmentCandidates")
            .Produces<DirectAssignmentCandidatesResponse>(200)
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json");

        group.MapPost("/", DirectAssignLeaderAsync)
            .WithName("DirectAssignLeader")
            .Accepts<DirectAssignLeaderRequest>("application/json")
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

    private static async Task<IResult> GetCandidatesAsync(
        string eventId,
        HttpContext context,
        IDirectAssignmentStore store,
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
            var result = await store.GetCandidatesAsync(parsedEventId, actorUserId, cancellationToken);
            if (result is null)
            {
                return ApiProblemDetailsService.Problem(context, 404, "event_not_found", "The specified event was not found.");
            }

            var candidateItems = result.Candidates
                .Select(c => new DirectAssignmentCandidateItemResponse(
                    c.UserId,
                    c.UserName,
                    c.AvailabilityStatus,
                    c.DeclaredAt))
                .ToList();

            return Results.Ok(new DirectAssignmentCandidatesResponse(
                EventId: result.EventId,
                StaffingCycleId: result.StaffingCycleId,
                IsWindowOpen: result.IsWindowOpen,
                Candidates: candidateItems));
        }
        catch (InvalidEventStatusException ex)
        {
            return ApiProblemDetailsService.Problem(context, 409, "invalid_event_status",
                $"Direct assignment is permitted only on Published events. Event {ex.EventId} is currently in {ex.CurrentStatus} status.");
        }
        catch (UnauthorizedAccessException ex)
        {
            return ApiProblemDetailsService.Problem(context, 403, "unauthorized_chief_leader", ex.Message);
        }
    }

    private static async Task<IResult> DirectAssignLeaderAsync(
        string eventId,
        DirectAssignLeaderRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IDirectAssignmentStore store,
        CancellationToken cancellationToken)
    {
        using var activity = DirectAssignmentTelemetry.Start();

        if (!Guid.TryParse(eventId, out var parsedEventId))
        {
            DirectAssignmentTelemetry.Record(activity, "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
        }

        if (request is null || request.CandidateUserId == Guid.Empty)
        {
            DirectAssignmentTelemetry.Record(activity, "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Candidate User identifier is required.");
        }

        if (!TryGetActor(context, out var actorUserId))
        {
            DirectAssignmentTelemetry.Record(activity, "unauthenticated");
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        var isAdmin = context.User.IsInRole(CarpediemRoles.Admin);

        try
        {
            var result = await store.DirectAssignLeaderAsync(parsedEventId, request.CandidateUserId, actorUserId, isAdmin, cancellationToken);

            DirectAssignmentTelemetry.Record(activity, result.IsIdempotentReplay ? "replay" : "assigned");

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

            return Results.Created($"/api/events/{result.EventId}/team-selection", responseBody);
        }
        catch (KeyNotFoundException ex)
        {
            DirectAssignmentTelemetry.Record(activity, "not_found");
            return ApiProblemDetailsService.Problem(context, 404, "not_found", ex.Message);
        }
        catch (InvalidEventStatusException ex)
        {
            DirectAssignmentTelemetry.Record(activity, "invalid_event_status");
            return ApiProblemDetailsService.Problem(context, 409, "invalid_event_status",
                $"Direct assignment is permitted only on Published events. Event {ex.EventId} is currently in {ex.CurrentStatus} status.");
        }
        catch (TeamSelectionWindowClosedException)
        {
            DirectAssignmentTelemetry.Record(activity, "window_closed");
            return ApiProblemDetailsService.Problem(context, 409, "window_closed",
                "Direct assignment is closed because the event has already started.");
        }
        catch (ChiefLeaderExclusivityException)
        {
            DirectAssignmentTelemetry.Record(activity, "chief_leader_conflict");
            return ApiProblemDetailsService.Problem(context, 409, "chief_leader_conflict",
                "The Chief Leader for this event cannot be assigned as a volunteer team member on this event.");
        }
        catch (UnauthorizedAccessException ex)
        {
            DirectAssignmentTelemetry.Record(activity, "unauthorized_chief_leader");
            return ApiProblemDetailsService.Problem(context, 403, "unauthorized_chief_leader", ex.Message);
        }
    }

    private static bool TryGetActor(HttpContext context, out Guid actorUserId)
    {
        var rawId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawId, out actorUserId);
    }
}

public sealed record DirectAssignmentCandidateItemResponse(
    Guid UserId,
    string UserName,
    string AvailabilityStatus,
    DateTimeOffset? DeclaredAt);

public sealed record DirectAssignmentCandidatesResponse(
    Guid EventId,
    Guid StaffingCycleId,
    bool IsWindowOpen,
    IReadOnlyList<DirectAssignmentCandidateItemResponse> Candidates);

public sealed record DirectAssignLeaderRequest(Guid CandidateUserId);
