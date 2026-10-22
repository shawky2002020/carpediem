using System.Security.Claims;
using Carpediem.Modules.Events.Application.Cancellation;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Web.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Web.Endpoints;

public static class EventCancellationEndpoints
{
    public static IEndpointRouteBuilder MapEventCancellationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events/{eventId}/cancellation")
            .WithTags("Event Cancellation")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);
        group.MapGet("/", GetContextAsync)
            .WithName("GetEventCancellationContext")
            .Produces<EventCancellationContext>(200);
        group.MapPost("/", CancelAsync)
            .WithName("CancelEvent")
            .WithMetadata(new RequireAntiforgeryTokenAttribute())
            .Produces<EventCancellationResult>(200)
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(412, "application/problem+json");
        return endpoints;
    }

    private static async Task<IResult> GetContextAsync(
        string eventId, HttpContext context, IEventCancellationStore store,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!Guid.TryParse(eventId, out var id)) return BadId(context);
        if (!TryGetActor(context, out var actorId)) return Unauthenticated(context);
        try
        {
            var result = await store.GetContextAsync(id, actorId, cancellationToken);
            return result is null ? NotFound(context) : Results.Ok(result);
        }
        catch (UnauthorizedAccessException) { return Forbidden(context); }
    }

    private static async Task<IResult> CancelAsync(
        string eventId, CancelEventRequest request, HttpContext context,
        IAntiforgery antiforgery, IEventCancellationStore store,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false } ||
            !await IsValidAntiforgeryAsync(context, antiforgery))
            return ApiProblemDetailsService.Problem(context, 400, "antiforgery_failed", "The request could not be verified.");
        if (!Guid.TryParse(eventId, out var id)) return BadId(context);
        if (!TryGetActor(context, out var actorId)) return Unauthenticated(context);
        if (request is null || string.IsNullOrWhiteSpace(request.Reason) ||
            request.Reason.Trim().Length is < 10 or > 500)
            return ApiProblemDetailsService.Problem(context, 400, "invalid_cancellation_reason",
                "A justification between 10 and 500 characters is required.");

        byte[] version;
        try
        {
            version = Convert.FromBase64String(context.Request.Headers.IfMatch.ToString().Trim().Trim('"'));
            if (version.Length != 8) throw new FormatException();
        }
        catch (FormatException)
        {
            return ApiProblemDetailsService.Problem(context, 400, "malformed_version", "A valid If-Match version is required.");
        }

        try
        {
            return Results.Ok(await store.CancelAsync(id, actorId, version,
                request.Reason, Guid.NewGuid(), clock.GetUtcNow(), cancellationToken));
        }
        catch (UnauthorizedAccessException) { return Forbidden(context); }
        catch (KeyNotFoundException) { return NotFound(context); }
        catch (InvalidOperationException)
        {
            return ApiProblemDetailsService.Problem(context, 409, "invalid_event_status",
                "Only Draft or Published Events can be cancelled.");
        }
        catch (DbUpdateConcurrencyException)
        {
            return ApiProblemDetailsService.Problem(context, 412, "concurrency_conflict", "The Event changed. Refresh and try again.");
        }
    }

    private static async Task<bool> IsValidAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try { await antiforgery.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static bool TryGetActor(HttpContext context, out Guid id) =>
        Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out id);
    private static IResult BadId(HttpContext context) => ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
    private static IResult Unauthenticated(HttpContext context) => ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
    private static IResult Forbidden(HttpContext context) => ApiProblemDetailsService.Problem(context, 403, "event_cancellation_forbidden", "Only an active Admin can cancel Events.");
    private static IResult NotFound(HttpContext context) => ApiProblemDetailsService.Problem(context, 404, "event_not_found", "Event not found.");
    private sealed record CancelEventRequest(string Reason);
}
