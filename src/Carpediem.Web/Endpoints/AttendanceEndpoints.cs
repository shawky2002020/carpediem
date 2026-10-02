using System.Security.Claims;
using Carpediem.Modules.Attendance.Application;
using Carpediem.Modules.Attendance.Domain;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Web.Errors;
using Carpediem.Web.Observability;
using Microsoft.AspNetCore.Antiforgery;

namespace Carpediem.Web.Endpoints;

public static class AttendanceEndpoints
{
    public static IEndpointRouteBuilder MapAttendanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events/{eventId}/attendance")
            .WithTags("Attendance")
            .RequireAuthorization(AuthorizationPolicies.RequireChiefLeaderOrAdmin);

        group.MapGet("/", GetRollSheetAsync)
            .WithName("GetAttendanceRollSheet")
            .Produces<AttendanceRollSheetResponse>(200)
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json");

        group.MapPost("/", FinalizeAttendanceAsync)
            .WithName("FinalizeAttendance")
            .Accepts<FinalizeAttendanceRequest>("application/json")
            .Produces<FinalizeAttendanceResponse>(200)
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());

        return endpoints;
    }

    private static async Task<IResult> GetRollSheetAsync(
        string eventId,
        HttpContext context,
        IAttendanceStore store,
        CancellationToken cancellationToken)
    {
        using var activity = AttendanceTelemetry.Source.StartActivity("GetRollSheet");

        if (!Guid.TryParse(eventId, out var parsedEventId))
        {
            AttendanceTelemetry.Record(activity, "GetRollSheet", "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
        }

        if (!TryGetActor(context, out var actorUserId))
        {
            AttendanceTelemetry.Record(activity, "GetRollSheet", "unauthenticated");
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        try
        {
            var result = await store.GetRollSheetAsync(parsedEventId, actorUserId, cancellationToken);
            if (result is null)
            {
                AttendanceTelemetry.Record(activity, "GetRollSheet", "event_not_found");
                return ApiProblemDetailsService.Problem(context, 404, "event_not_found", $"Event {parsedEventId} was not found.");
            }

            AttendanceTelemetry.Record(activity, "GetRollSheet", "success");

            var response = new AttendanceRollSheetResponse(
                EventId: result.EventId,
                EventTitle: result.EventTitle,
                Status: result.Status,
                StartDateTime: result.StartDateTime,
                EndDateTime: result.EndDateTime,
                Location: result.Location,
                StaffingCycleId: result.StaffingCycleId,
                IsCompleted: result.IsCompleted,
                IsAllFinalized: result.IsAllFinalized,
                Entries: result.Entries.Select(e => new AttendanceRollSheetEntryResponse(
                    TeamAssignmentId: e.TeamAssignmentId,
                    VolunteerUserId: e.VolunteerUserId,
                    VolunteerName: e.VolunteerName,
                    Outcome: e.Outcome,
                    FinalizedAt: e.FinalizedAt,
                    Version: e.RowVersion is not null ? Convert.ToBase64String(e.RowVersion) : null
                )).ToList());

            return Results.Ok(response);
        }
        catch (InvalidEventStatusException ex)
        {
            AttendanceTelemetry.Record(activity, "GetRollSheet", "invalid_event_status");
            return ApiProblemDetailsService.Problem(context, 409, "invalid_event_status", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            AttendanceTelemetry.Record(activity, "GetRollSheet", "unauthorized_chief_leader");
            return ApiProblemDetailsService.Problem(context, 403, "unauthorized_chief_leader", ex.Message);
        }
    }

    private static async Task<IResult> FinalizeAttendanceAsync(
        string eventId,
        FinalizeAttendanceRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IAttendanceStore store,
        CancellationToken cancellationToken)
    {
        using var activity = AttendanceTelemetry.Source.StartActivity("FinalizeAttendance");

        if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false } ||
            !await HasValidAntiforgeryAsync(context, antiforgery))
        {
            AttendanceTelemetry.Record(activity, "FinalizeAttendance", "antiforgery_failed");
            return ApiProblemDetailsService.Problem(context, 400, "antiforgery_failed", "The request could not be verified.");
        }

        if (!Guid.TryParse(eventId, out var parsedEventId))
        {
            AttendanceTelemetry.Record(activity, "FinalizeAttendance", "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
        }

        if (request is null || request.Entries is null || request.Entries.Count == 0)
        {
            AttendanceTelemetry.Record(activity, "FinalizeAttendance", "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "At least one attendance outcome entry is required.");
        }

        if (!TryGetActor(context, out var actorUserId))
        {
            AttendanceTelemetry.Record(activity, "FinalizeAttendance", "unauthenticated");
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        var correlationId = Guid.NewGuid();
        var utcNow = DateTimeOffset.UtcNow;
        var items = request.Entries.Select(e => new FinalizeAttendanceItem(e.TeamAssignmentId, e.Outcome)).ToList();

        try
        {
            var result = await store.FinalizeAttendanceAsync(
                parsedEventId,
                items,
                actorUserId,
                correlationId,
                utcNow,
                cancellationToken);

            AttendanceTelemetry.Record(activity, "FinalizeAttendance", "success");

            return Results.Ok(new FinalizeAttendanceResponse(
                EventId: result.EventId,
                FinalizedAt: result.FinalizedAt,
                TotalActiveAssignments: result.TotalActiveAssignments,
                AttendedCount: result.AttendedCount,
                NoShowCount: result.NoShowCount,
                UnrecordedCount: result.UnrecordedCount));
        }
        catch (KeyNotFoundException)
        {
            AttendanceTelemetry.Record(activity, "FinalizeAttendance", "event_not_found");
            return ApiProblemDetailsService.Problem(context, 404, "event_not_found", $"Event {parsedEventId} was not found.");
        }
        catch (InvalidEventStatusException ex)
        {
            AttendanceTelemetry.Record(activity, "FinalizeAttendance", "invalid_event_status");
            return ApiProblemDetailsService.Problem(context, 409, "invalid_event_status", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            AttendanceTelemetry.Record(activity, "FinalizeAttendance", "unauthorized_chief_leader");
            return ApiProblemDetailsService.Problem(context, 403, "unauthorized_chief_leader", ex.Message);
        }
        catch (ArgumentException ex)
        {
            AttendanceTelemetry.Record(activity, "FinalizeAttendance", "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            AttendanceTelemetry.Record(activity, "FinalizeAttendance", "conflict");
            return ApiProblemDetailsService.Problem(context, 409, "conflict", ex.Message);
        }
    }

    private static bool TryGetActor(HttpContext context, out Guid actorUserId)
    {
        var rawId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(rawId, out actorUserId);
    }

    private static async Task<bool> HasValidAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }
}

public sealed record AttendanceRollSheetResponse(
    Guid EventId,
    string EventTitle,
    string Status,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    Guid StaffingCycleId,
    bool IsCompleted,
    bool IsAllFinalized,
    IReadOnlyList<AttendanceRollSheetEntryResponse> Entries);

public sealed record AttendanceRollSheetEntryResponse(
    Guid TeamAssignmentId,
    Guid VolunteerUserId,
    string VolunteerName,
    string? Outcome,
    DateTimeOffset? FinalizedAt,
    string? Version);

public sealed record FinalizeAttendanceRequest(
    IReadOnlyList<AttendanceOutcomeSubmission> Entries);

public sealed record AttendanceOutcomeSubmission(
    Guid TeamAssignmentId,
    string Outcome);

public sealed record FinalizeAttendanceResponse(
    Guid EventId,
    DateTimeOffset FinalizedAt,
    int TotalActiveAssignments,
    int AttendedCount,
    int NoShowCount,
    int UnrecordedCount);
