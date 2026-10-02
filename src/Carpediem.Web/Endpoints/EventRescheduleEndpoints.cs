using System.Security.Claims;
using Carpediem.Modules.Events.Application.Reschedule;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Web.Errors;
using Carpediem.Web.Observability;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Web.Endpoints;

public static class EventRescheduleEndpoints
{
    public static IEndpointRouteBuilder MapEventRescheduleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events/{eventId}/reschedule")
            .WithTags("Event Reschedule")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        group.MapPost("/", RescheduleEventAsync)
            .WithName("RescheduleEvent")
            .Accepts<RescheduleEventRequest>("application/json")
            .Produces<RescheduleEventResponse>(200)
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

    private static async Task<IResult> RescheduleEventAsync(
        string eventId,
        RescheduleEventRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IEventRescheduleStore store,
        CancellationToken cancellationToken)
    {
        using var activity = EventRescheduleTelemetry.Source.StartActivity("RescheduleEvent");

        if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false } ||
            !await HasValidAntiforgeryAsync(context, antiforgery))
        {
            EventRescheduleTelemetry.Record(activity, "antiforgery_failed");
            return ApiProblemDetailsService.Problem(context, 400, "antiforgery_failed", "The request could not be verified.");
        }

        if (!Guid.TryParse(eventId, out var parsedEventId))
        {
            EventRescheduleTelemetry.Record(activity, "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
        }

        if (!context.Request.Headers.TryGetValue("If-Match", out var ifMatchHeader) ||
            string.IsNullOrWhiteSpace(ifMatchHeader.ToString()))
        {
            EventRescheduleTelemetry.Record(activity, "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "If-Match header is required.");
        }

        var cleanVersion = ifMatchHeader.ToString().Trim('\"', ' ');
        byte[] expectedVersion;
        try
        {
            expectedVersion = Convert.FromBase64String(cleanVersion);
            if (expectedVersion.Length != 8) throw new FormatException("Invalid version length.");
        }
        catch (FormatException)
        {
            EventRescheduleTelemetry.Record(activity, "malformed_version");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_version", "A valid base64 version is required in the If-Match header.");
        }

        if (!TryGetActor(context, out var actorUserId))
        {
            EventRescheduleTelemetry.Record(activity, "unauthenticated");
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        if (!context.User.IsInRole(CarpediemRoles.Admin))
        {
            EventRescheduleTelemetry.Record(activity, "unauthorized_administrator");
            return ApiProblemDetailsService.Problem(context, 403, "unauthorized_administrator", "Only Platform Administrators can reschedule events.");
        }

        if (request is null)
        {
            EventRescheduleTelemetry.Record(activity, "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid request body is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Justification) || request.Justification.Trim().Length < 10 || request.Justification.Trim().Length > 500)
        {
            EventRescheduleTelemetry.Record(activity, "invalid_justification");
            return ApiProblemDetailsService.Problem(context, 400, "invalid_justification", "Administrative justification must be between 10 and 500 characters.");
        }

        var now = DateTimeOffset.UtcNow;
        if (request.NewStartDateTime <= now)
        {
            EventRescheduleTelemetry.Record(activity, "invalid_schedule");
            return ApiProblemDetailsService.Problem(context, 400, "invalid_schedule", "New start time must be in the future.");
        }

        if (request.NewEndDateTime <= request.NewStartDateTime)
        {
            EventRescheduleTelemetry.Record(activity, "invalid_schedule");
            return ApiProblemDetailsService.Problem(context, 400, "invalid_schedule", "New end time must be after the new start time.");
        }

        if (request.NewStartDateTime.Offset != TimeSpan.Zero || request.NewEndDateTime.Offset != TimeSpan.Zero)
        {
            EventRescheduleTelemetry.Record(activity, "invalid_schedule");
            return ApiProblemDetailsService.Problem(context, 400, "invalid_schedule", "Schedule times must be in UTC.");
        }

        var correlationId = context.Items.TryGetValue("CorrelationId", out var item) && item is Guid guid
            ? guid
            : Guid.NewGuid();

        try
        {
            var result = await store.RescheduleEventAsync(
                parsedEventId,
                request.NewStartDateTime,
                request.NewEndDateTime,
                request.Justification.Trim(),
                expectedVersion,
                actorUserId,
                correlationId,
                cancellationToken);

            EventRescheduleTelemetry.Record(activity, "rescheduled");

            var responseBody = new RescheduleEventResponse(
                EventId: result.EventId,
                Title: result.Title,
                Status: result.Status,
                StartDateTime: result.StartDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                EndDateTime: result.EndDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                Location: result.Location,
                Notes: result.Notes,
                ActiveStaffingCycleId: result.ActiveStaffingCycleId,
                CycleNumber: result.CycleNumber,
                Version: Convert.ToBase64String(result.Version),
                RescheduledAt: result.RescheduledAt.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
                NotificationsDispatched: result.NotificationsDispatched);

            return Results.Ok(responseBody);
        }
        catch (KeyNotFoundException ex)
        {
            EventRescheduleTelemetry.Record(activity, "not_found");
            return ApiProblemDetailsService.Problem(context, 404, "not_found", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            EventRescheduleTelemetry.Record(activity, "unauthorized_administrator");
            return ApiProblemDetailsService.Problem(context, 403, "unauthorized_administrator", ex.Message);
        }
        catch (InvalidEventStatusForRescheduleException ex)
        {
            EventRescheduleTelemetry.Record(activity, "invalid_event_status");
            return ApiProblemDetailsService.Problem(context, 409, "invalid_event_status",
                $"Rescheduling requires a Published event. Event {ex.EventId} is currently in {ex.CurrentStatus} status.");
        }
        catch (EventRescheduleWindowClosedException)
        {
            EventRescheduleTelemetry.Record(activity, "window_closed");
            return ApiProblemDetailsService.Problem(context, 409, "window_closed",
                "The event cannot be rescheduled because its scheduled start time has already passed.");
        }
        catch (DbUpdateConcurrencyException)
        {
            EventRescheduleTelemetry.Record(activity, "concurrency_conflict");
            return ApiProblemDetailsService.Problem(context, 412, "concurrency_conflict",
                "The event was modified concurrently. Please refresh the latest state and retry.");
        }
        catch (ArgumentException ex)
        {
            EventRescheduleTelemetry.Record(activity, "invalid_operation");
            return ApiProblemDetailsService.Problem(context, 400, "invalid_operation", ex.Message);
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

public sealed record RescheduleEventRequest(
    DateTimeOffset NewStartDateTime,
    DateTimeOffset NewEndDateTime,
    string Justification);

public sealed record RescheduleEventResponse(
    Guid EventId,
    string Title,
    string Status,
    string StartDateTime,
    string EndDateTime,
    string Location,
    string? Notes,
    Guid ActiveStaffingCycleId,
    int CycleNumber,
    string Version,
    string RescheduledAt,
    int NotificationsDispatched);
