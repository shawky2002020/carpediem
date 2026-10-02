using System.Security.Claims;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Application.SelfCancellation;
using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Carpediem.Web.Errors;
using Carpediem.Web.Observability;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Web.Endpoints;

public static class EventSelfCancellationEndpoints
{
    public static IEndpointRouteBuilder MapEventSelfCancellationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events/{eventId}")
            .WithTags("Self Cancellation")
            .RequireAuthorization(AuthorizationPolicies.RequireLeaderOrAdmin);

        group.MapGet("/my-assignment", GetMyAssignmentAsync)
            .WithName("GetMyAssignmentContext")
            .Produces<MyAssignmentResponse>(200)
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json");

        group.MapPost("/assignments/{assignmentId}/cancel", CancelAssignmentAsync)
            .WithName("CancelAssignment")
            .Accepts<CancelAssignmentRequest>("application/json")
            .Produces<CancelledAssignmentResponse>(200)
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(412, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());

        return endpoints;
    }

    private static async Task<IResult> GetMyAssignmentAsync(
        string eventId,
        HttpContext context,
        ISelfCancellationStore store,
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
            var result = await store.GetMyAssignmentAsync(parsedEventId, actorUserId, cancellationToken);
            if (result is null)
            {
                return ApiProblemDetailsService.Problem(context, 404, "assignment_not_found", "No assignment was found for the current user on this event.");
            }

            return Results.Ok(new MyAssignmentResponse(
                AssignmentId: result.AssignmentId,
                EventId: result.EventId,
                StaffingCycleId: result.StaffingCycleId,
                UserId: result.UserId,
                UserName: result.UserName,
                EventTitle: result.EventTitle,
                StartDateTime: result.StartDateTime,
                EndDateTime: result.EndDateTime,
                Location: result.Location,
                Status: result.Status,
                AssignedAt: result.AssignedAt,
                CancelledAt: result.CancelledAt,
                CancellationReason: result.CancellationReason,
                IsWindowOpen: result.IsWindowOpen,
                Version: result.Version));
        }
        catch (InvalidEventStatusException ex)
        {
            return ApiProblemDetailsService.Problem(context, 409, "invalid_event_status",
                $"Assignment access is permitted only on Published events. Event {ex.EventId} is currently in {ex.CurrentStatus} status.");
        }
    }

    private static async Task<IResult> CancelAssignmentAsync(
        string eventId,
        string assignmentId,
        CancelAssignmentRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        ISelfCancellationStore store,
        CancellationToken cancellationToken)
    {
        using var activity = LeaderSelfCancellationTelemetry.Start();

        if (!Guid.TryParse(eventId, out var parsedEventId))
        {
            LeaderSelfCancellationTelemetry.Record(activity, "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
        }

        if (!Guid.TryParse(assignmentId, out var parsedAssignmentId))
        {
            LeaderSelfCancellationTelemetry.Record(activity, "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Assignment identifier is required.");
        }

        if (string.IsNullOrWhiteSpace(request?.Justification))
        {
            LeaderSelfCancellationTelemetry.Record(activity, "invalid_justification");
            return ApiProblemDetailsService.Problem(context, 400, "invalid_justification", "A non-empty cancellation justification is required.");
        }

        var trimmedReason = request.Justification.Trim();
        if (trimmedReason.Length > 500)
        {
            LeaderSelfCancellationTelemetry.Record(activity, "invalid_justification");
            return ApiProblemDetailsService.Problem(context, 400, "invalid_justification", "Cancellation justification cannot exceed 500 characters.");
        }

        if (!TryGetActor(context, out var actorUserId))
        {
            LeaderSelfCancellationTelemetry.Record(activity, "unauthenticated");
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        var isAdmin = context.User.IsInRole(CarpediemRoles.Admin);

        byte[]? expectedRowVersion = null;
        if (context.Request.Headers.TryGetValue("If-Match", out var ifMatchHeader))
        {
            var headerValue = ifMatchHeader.ToString().Trim('\"', ' ');
            if (!string.IsNullOrWhiteSpace(headerValue))
            {
                try
                {
                    expectedRowVersion = Convert.FromBase64String(headerValue);
                }
                catch (FormatException)
                {
                    LeaderSelfCancellationTelemetry.Record(activity, "malformed_version");
                    return ApiProblemDetailsService.Problem(context, 400, "malformed_version", "The provided If-Match header is not a valid base64 concurrency token.");
                }
            }
        }

        try
        {
            var result = await store.CancelAssignmentAsync(
                parsedEventId,
                parsedAssignmentId,
                actorUserId,
                trimmedReason,
                expectedRowVersion,
                isAdmin,
                cancellationToken);

            LeaderSelfCancellationTelemetry.Record(activity, result.IsIdempotentReplay ? "replay" : "cancelled");

            var responseBody = new CancelledAssignmentResponse(
                Id: result.Id,
                EventId: result.EventId,
                StaffingCycleId: result.StaffingCycleId,
                UserId: result.UserId,
                Status: result.Status,
                AssignedAt: result.AssignedAt,
                AssignedByUserId: result.AssignedByUserId,
                CancellationReason: result.CancellationReason,
                CancelledAt: result.CancelledAt,
                Version: result.Version);

            if (result.IsIdempotentReplay)
            {
                context.Response.Headers.Append("Idempotent-Replay", "true");
            }

            return Results.Ok(responseBody);
        }
        catch (KeyNotFoundException ex)
        {
            LeaderSelfCancellationTelemetry.Record(activity, "not_found");
            return ApiProblemDetailsService.Problem(context, 404, "not_found", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            LeaderSelfCancellationTelemetry.Record(activity, "unauthorized_assignment_owner");
            return ApiProblemDetailsService.Problem(context, 403, "unauthorized_assignment_owner", ex.Message);
        }
        catch (InvalidEventStatusException ex)
        {
            LeaderSelfCancellationTelemetry.Record(activity, "invalid_event_status");
            return ApiProblemDetailsService.Problem(context, 409, "invalid_event_status",
                $"Assignment cancellation is permitted only on Published events. Event {ex.EventId} is currently in {ex.CurrentStatus} status.");
        }
        catch (CancellationWindowClosedException)
        {
            LeaderSelfCancellationTelemetry.Record(activity, "window_closed");
            return ApiProblemDetailsService.Problem(context, 409, "window_closed",
                "Assignment cancellation is closed because the event has already started.");
        }
        catch (AssignmentAlreadyCancelledException)
        {
            LeaderSelfCancellationTelemetry.Record(activity, "assignment_already_cancelled");
            return ApiProblemDetailsService.Problem(context, 409, "assignment_already_cancelled",
                "The assignment has already been cancelled.");
        }
        catch (DbUpdateConcurrencyException)
        {
            LeaderSelfCancellationTelemetry.Record(activity, "concurrency_conflict");
            return ApiProblemDetailsService.Problem(context, 412, "concurrency_conflict",
                "The assignment was modified concurrently. Please refresh the latest state and retry.");
        }
        catch (ArgumentException ex)
        {
            LeaderSelfCancellationTelemetry.Record(activity, "invalid_justification");
            return ApiProblemDetailsService.Problem(context, 400, "invalid_justification", ex.Message);
        }
    }

    private static bool TryGetActor(HttpContext context, out Guid actorUserId)
    {
        var rawId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawId, out actorUserId);
    }
}

public sealed record CancelAssignmentRequest(string Justification);

public sealed record MyAssignmentResponse(
    Guid AssignmentId,
    Guid EventId,
    Guid StaffingCycleId,
    Guid UserId,
    string UserName,
    string EventTitle,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    string Status,
    DateTimeOffset AssignedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    bool IsWindowOpen,
    string Version);

public sealed record CancelledAssignmentResponse(
    Guid Id,
    Guid EventId,
    Guid StaffingCycleId,
    Guid UserId,
    string Status,
    DateTimeOffset AssignedAt,
    Guid AssignedByUserId,
    string CancellationReason,
    DateTimeOffset CancelledAt,
    string Version);
