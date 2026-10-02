using System.Security.Claims;
using Carpediem.Modules.Events.Application.Completion;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Web.Errors;
using Carpediem.Web.Observability;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Web.Endpoints;

public static class EventCompletionEndpoints
{
    public static IEndpointRouteBuilder MapEventCompletionEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events/{eventId}/complete")
            .WithTags("Event Completion")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        group.MapPost("/", CompleteEventAsync)
            .WithName("CompleteEvent")
            .Produces<CompleteEventResponse>(200)
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

    private static async Task<IResult> CompleteEventAsync(
        string eventId,
        HttpContext context,
        IAntiforgery antiforgery,
        IEventCompletionStore store,
        CancellationToken cancellationToken)
    {
        using var activity = EventCompletionTelemetry.Source.StartActivity("ManualCloseout");

        if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false } ||
            !await HasValidAntiforgeryAsync(context, antiforgery))
        {
            EventCompletionTelemetry.Record(activity, "AdminManualCloseout", "antiforgery_failed");
            return ApiProblemDetailsService.Problem(context, 400, "antiforgery_failed", "The request could not be verified.");
        }

        if (!Guid.TryParse(eventId, out var parsedEventId))
        {
            EventCompletionTelemetry.Record(activity, "AdminManualCloseout", "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
        }

        if (!context.Request.Headers.TryGetValue("If-Match", out var ifMatchHeader) ||
            string.IsNullOrWhiteSpace(ifMatchHeader.ToString()))
        {
            EventCompletionTelemetry.Record(activity, "AdminManualCloseout", "malformed_request");
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
            EventCompletionTelemetry.Record(activity, "AdminManualCloseout", "malformed_version");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_version", "A valid base64 version is required in the If-Match header.");
        }

        if (!TryGetActor(context, out var actorUserId))
        {
            EventCompletionTelemetry.Record(activity, "AdminManualCloseout", "unauthenticated");
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        if (!context.User.IsInRole(CarpediemRoles.Admin))
        {
            EventCompletionTelemetry.Record(activity, "AdminManualCloseout", "unauthorized_administrator");
            return ApiProblemDetailsService.Problem(context, 403, "unauthorized_administrator", "Only Platform Administrators can complete events.");
        }

        var correlationId = Guid.NewGuid();
        var utcNow = DateTimeOffset.UtcNow;

        try
        {
            var result = await store.CompleteEventManuallyAsync(
                parsedEventId,
                expectedVersion,
                actorUserId,
                correlationId,
                utcNow,
                cancellationToken);

            var versionBase64 = Convert.ToBase64String(result.Version);
            context.Response.Headers.ETag = $"\"{versionBase64}\"";

            if (result.IsReplay)
            {
                context.Response.Headers["Idempotent-Replay"] = "true";
                EventCompletionTelemetry.Record(activity, "AdminManualCloseout", "idempotent_replay");
            }
            else
            {
                EventCompletionTelemetry.Record(activity, "AdminManualCloseout", "completed");
            }

            var response = new CompleteEventResponse(
                result.EventId,
                result.Title,
                result.Status,
                result.StartDateTime,
                result.EndDateTime,
                result.Location,
                result.Notes,
                result.CompletedAt,
                versionBase64);

            return Results.Ok(response);
        }
        catch (UnauthorizedAccessException)
        {
            EventCompletionTelemetry.Record(activity, "AdminManualCloseout", "unauthorized_administrator");
            return ApiProblemDetailsService.Problem(context, 403, "unauthorized_administrator", "Only Platform Administrators can complete events.");
        }
        catch (KeyNotFoundException)
        {
            EventCompletionTelemetry.Record(activity, "AdminManualCloseout", "event_not_found");
            return ApiProblemDetailsService.Problem(context, 404, "event_not_found", $"Event {parsedEventId} was not found.");
        }
        catch (InvalidEventStatusForCompletionException ex)
        {
            EventCompletionTelemetry.Record(activity, "AdminManualCloseout", "invalid_event_status");
            return ApiProblemDetailsService.Problem(context, 409, "invalid_event_status", ex.Message);
        }
        catch (DbUpdateConcurrencyException)
        {
            EventCompletionTelemetry.Record(activity, "AdminManualCloseout", "concurrency_conflict");
            return ApiProblemDetailsService.Problem(context, 412, "concurrency_conflict", "The event was modified concurrently. Please refresh.");
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

public sealed record CompleteEventResponse(
    Guid EventId,
    string Title,
    string Status,
    DateTimeOffset StartDateTime,
    DateTimeOffset EndDateTime,
    string Location,
    string? Notes,
    DateTimeOffset CompletedAt,
    string Version);
