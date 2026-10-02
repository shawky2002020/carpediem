using System.Security.Claims;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Application.ReplacementAssignment;
using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Carpediem.Web.Errors;
using Carpediem.Web.Observability;
using Microsoft.AspNetCore.Antiforgery;

namespace Carpediem.Web.Endpoints;

public static class EventReplacementAssignmentEndpoints
{
    public static IEndpointRouteBuilder MapEventReplacementAssignmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events/{eventId}/replacement-assignment")
            .WithTags("Replacement Assignment")
            .RequireAuthorization(AuthorizationPolicies.RequireChiefLeaderOrAdmin);

        group.MapGet("/", GetReplacementAssignmentContextAsync)
            .WithName("GetReplacementAssignmentContext")
            .Produces<ReplacementAssignmentContextResponse>(200)
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json");

        group.MapPost("/", AssignReplacementLeaderAsync)
            .WithName("AssignReplacementLeader")
            .Accepts<AssignReplacementLeaderRequest>("application/json")
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

    private static async Task<IResult> GetReplacementAssignmentContextAsync(
        string eventId,
        HttpContext context,
        IReplacementAssignmentStore store,
        CancellationToken cancellationToken)
    {
        using var activity = ReplacementAssignmentTelemetry.Source.StartActivity("GetReplacementAssignmentContext");

        if (!Guid.TryParse(eventId, out var parsedEventId))
        {
            ReplacementAssignmentTelemetry.Record(activity, "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
        }

        if (!TryGetActor(context, out var actorUserId))
        {
            ReplacementAssignmentTelemetry.Record(activity, "unauthenticated");
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        var isAdmin = context.User.IsInRole(CarpediemRoles.Admin);

        try
        {
            var result = await store.GetReplacementContextAsync(parsedEventId, actorUserId, isAdmin, cancellationToken);
            ReplacementAssignmentTelemetry.Record(activity, "context_queried");

            var responseBody = new ReplacementAssignmentContextResponse(
                EventId: result.EventId,
                EventTitle: result.EventTitle,
                StaffingCycleId: result.StaffingCycleId,
                StartDateTime: result.StartDateTime,
                EndDateTime: result.EndDateTime,
                Location: result.Location,
                IsWindowOpen: result.IsWindowOpen,
                ActiveTeam: result.ActiveTeam.Select(m => new ReplacementTeamMemberItemResponse(
                    m.AssignmentId,
                    m.UserId,
                    m.UserName,
                    m.Status,
                    m.AssignedAt,
                    m.Version)).ToList(),
                CancelledAssignments: result.CancelledAssignments.Select(m => new ReplacementCancelledMemberItemResponse(
                    m.AssignmentId,
                    m.UserId,
                    m.UserName,
                    m.Status,
                    m.CancelledAt,
                    m.CancellationReason)).ToList(),
                Candidates: result.Candidates.Select(c => new ReplacementCandidateItemResponse(
                    c.UserId,
                    c.UserName,
                    c.AvailabilityStatus,
                    c.DeclaredAt)).ToList());

            return Results.Ok(responseBody);
        }
        catch (KeyNotFoundException ex)
        {
            ReplacementAssignmentTelemetry.Record(activity, "not_found");
            return ApiProblemDetailsService.Problem(context, 404, "not_found", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            ReplacementAssignmentTelemetry.Record(activity, "unauthorized_chief_leader");
            return ApiProblemDetailsService.Problem(context, 403, "unauthorized_chief_leader", ex.Message);
        }
        catch (InvalidEventStatusException ex)
        {
            ReplacementAssignmentTelemetry.Record(activity, "invalid_event_status");
            return ApiProblemDetailsService.Problem(context, 409, "invalid_event_status",
                $"Replacement assignment requires a Published event. Event {ex.EventId} is currently in {ex.CurrentStatus} status.");
        }
    }

    private static async Task<IResult> AssignReplacementLeaderAsync(
        string eventId,
        AssignReplacementLeaderRequest request,
        HttpContext context,
        IReplacementAssignmentStore store,
        CancellationToken cancellationToken)
    {
        using var activity = ReplacementAssignmentTelemetry.Source.StartActivity("AssignReplacementLeader");

        if (!Guid.TryParse(eventId, out var parsedEventId))
        {
            ReplacementAssignmentTelemetry.Record(activity, "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
        }

        if (request is null || request.CandidateUserId == Guid.Empty)
        {
            ReplacementAssignmentTelemetry.Record(activity, "invalid_candidate");
            return ApiProblemDetailsService.Problem(context, 400, "invalid_candidate", "A valid candidate user identifier is required.");
        }

        if (request.Justification is not null && request.Justification.Trim().Length > 500)
        {
            ReplacementAssignmentTelemetry.Record(activity, "invalid_justification");
            return ApiProblemDetailsService.Problem(context, 400, "invalid_justification", "Justification reason cannot exceed 500 characters.");
        }

        if (!TryGetActor(context, out var actorUserId))
        {
            ReplacementAssignmentTelemetry.Record(activity, "unauthenticated");
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        var isAdmin = context.User.IsInRole(CarpediemRoles.Admin);

        try
        {
            var result = await store.AssignReplacementLeaderAsync(
                parsedEventId,
                request.CandidateUserId,
                actorUserId,
                request.ReplacedAssignmentId,
                request.Justification?.Trim(),
                isAdmin,
                cancellationToken);

            ReplacementAssignmentTelemetry.Record(activity, result.IsIdempotentReplay ? "replay" : "assigned");

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

            return Results.Created($"/api/events/{eventId}/replacement-assignment/{result.Id}", responseBody);
        }
        catch (KeyNotFoundException ex)
        {
            ReplacementAssignmentTelemetry.Record(activity, "not_found");
            return ApiProblemDetailsService.Problem(context, 404, "not_found", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            ReplacementAssignmentTelemetry.Record(activity, "unauthorized_chief_leader");
            return ApiProblemDetailsService.Problem(context, 403, "unauthorized_chief_leader", ex.Message);
        }
        catch (InvalidEventStatusException ex)
        {
            ReplacementAssignmentTelemetry.Record(activity, "invalid_event_status");
            return ApiProblemDetailsService.Problem(context, 409, "invalid_event_status",
                $"Replacement assignment requires a Published event. Event {ex.EventId} is currently in {ex.CurrentStatus} status.");
        }
        catch (TeamSelectionWindowClosedException)
        {
            ReplacementAssignmentTelemetry.Record(activity, "window_closed");
            return ApiProblemDetailsService.Problem(context, 409, "window_closed", "The replacement assignment window is closed because the event has already started.");
        }
        catch (ChiefLeaderExclusivityException)
        {
            ReplacementAssignmentTelemetry.Record(activity, "chief_leader_conflict");
            return ApiProblemDetailsService.Problem(context, 409, "chief_leader_conflict", "The Chief Leader cannot be selected as a volunteer team member on this event.");
        }
        catch (InvalidOperationException ex)
        {
            ReplacementAssignmentTelemetry.Record(activity, "invalid_operation");
            return ApiProblemDetailsService.Problem(context, 400, "invalid_operation", ex.Message);
        }
    }

    private static bool TryGetActor(HttpContext context, out Guid actorUserId)
    {
        var rawId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawId, out actorUserId);
    }
}

public sealed record ReplacementTeamMemberItemResponse(
    Guid AssignmentId,
    Guid UserId,
    string UserName,
    string Status,
    DateTimeOffset AssignedAt,
    string Version);

public sealed record ReplacementCancelledMemberItemResponse(
    Guid AssignmentId,
    Guid UserId,
    string UserName,
    string Status,
    DateTimeOffset CancelledAt,
    string CancellationReason);

public sealed record ReplacementCandidateItemResponse(
    Guid UserId,
    string UserName,
    string AvailabilityStatus,
    DateTimeOffset? DeclaredAt);

public sealed record ReplacementAssignmentContextResponse(
    Guid EventId,
    string EventTitle,
    Guid StaffingCycleId,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    bool IsWindowOpen,
    IReadOnlyList<ReplacementTeamMemberItemResponse> ActiveTeam,
    IReadOnlyList<ReplacementCancelledMemberItemResponse> CancelledAssignments,
    IReadOnlyList<ReplacementCandidateItemResponse> Candidates);

public sealed record AssignReplacementLeaderRequest(
    Guid CandidateUserId,
    Guid? ReplacedAssignmentId = null,
    string? Justification = null);
