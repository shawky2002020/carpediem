using System.Globalization;
using System.Security.Claims;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Application.Availability;
using Carpediem.Modules.Staffing.Domain.Availability;
using Carpediem.Web.Errors;
using Carpediem.Web.Observability;
using Microsoft.AspNetCore.Antiforgery;

namespace Carpediem.Web.Endpoints;

public static class EventAvailabilityEndpoints
{
    public static IEndpointRouteBuilder MapEventAvailabilityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events/{eventId}/availability")
            .WithTags("Leader Availability")
            .RequireAuthorization(AuthorizationPolicies.RequireLeader);

        group.MapGet("/me", GetMyAvailabilityAsync)
            .WithName("GetMyAvailability")
            .Produces<MyAvailabilityResponse>(200)
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json");

        group.MapPost("/", DeclareAvailabilityAsync)
            .WithName("DeclareAvailability")
            .Accepts<DeclareAvailabilityRequest>("application/json")
            .Produces<AvailabilityDeclarationResponse>(200)
            .Produces<AvailabilityDeclarationResponse>(201)
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(422, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());

        return endpoints;
    }

    private static async Task<IResult> GetMyAvailabilityAsync(
        string eventId,
        HttpContext context,
        IAvailabilityStore store,
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

        var result = await store.GetMyAvailabilityAsync(parsedEventId, actorUserId, cancellationToken);
        if (result is null)
        {
            return ApiProblemDetailsService.Problem(context, 404, "event_not_found", "The specified event was not found.");
        }

        return Results.Ok(new MyAvailabilityResponse(
            EventId: result.EventId,
            StaffingCycleId: result.StaffingCycleId,
            Response: result.Response?.ToString(),
            DeclaredAt: result.DeclaredAt,
            Version: result.Version,
            IsWindowOpen: result.IsWindowOpen,
            EventStatus: result.EventStatus,
            IsChiefLeaderOnEvent: result.IsChiefLeaderOnEvent));
    }

    private static async Task<IResult> DeclareAvailabilityAsync(
        string eventId,
        DeclareAvailabilityRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        IAvailabilityStore store,
        CancellationToken cancellationToken)
    {
        using var activity = EventAvailabilityTelemetry.Start();

        if (!Guid.TryParse(eventId, out var parsedEventId))
        {
            EventAvailabilityTelemetry.Record(activity, "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
        }

        if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false } ||
            !await HasValidAntiforgeryAsync(context, antiforgery))
        {
            EventAvailabilityTelemetry.Record(activity, "antiforgery_failed");
            return ApiProblemDetailsService.Problem(context, 400, "antiforgery_failed", "The request could not be verified.");
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Response) ||
            !Enum.TryParse<AvailabilityResponseType>(request.Response, ignoreCase: true, out var responseType))
        {
            EventAvailabilityTelemetry.Record(activity, "invalid_payload");
            return ApiProblemDetailsService.Problem(context, 422, "validation_failed", "A valid availability response ('Available' or 'NotAvailable') is required.",
                errors: new Dictionary<string, string[]>
                {
                    ["response"] = ["Value must be either 'Available' or 'NotAvailable'."]
                });
        }

        if (!TryGetActor(context, out var actorUserId))
        {
            EventAvailabilityTelemetry.Record(activity, "unauthenticated");
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        byte[]? expectedVersion = null;
        var ifMatch = context.Request.Headers.IfMatch.ToString();
        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            var clean = ifMatch.Trim().Trim('"');
            try
            {
                expectedVersion = Convert.FromBase64String(clean);
                if (expectedVersion.Length != 8) throw new FormatException("Version length must be 8 bytes.");
            }
            catch (FormatException)
            {
                EventAvailabilityTelemetry.Record(activity, "invalid_if_match");
                return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid version is required in the If-Match header.");
            }
        }

        var correlationId = context.Items.TryGetValue("CorrelationId", out var item) && item is Guid guid
            ? guid
            : Guid.NewGuid();

        try
        {
            var command = new DeclareAvailabilityCommand(
                EventId: parsedEventId,
                UserId: actorUserId,
                Response: responseType,
                RowVersion: expectedVersion,
                CorrelationId: correlationId);

            var result = await store.DeclareAvailabilityAsync(command, cancellationToken);

            var responseBody = new AvailabilityDeclarationResponse(
                Id: result.Id,
                EventId: result.EventId,
                StaffingCycleId: result.StaffingCycleId,
                UserId: result.UserId,
                Response: result.Response.ToString(),
                DeclaredAt: result.DeclaredAt,
                Version: result.Version);

            if (result.IsIdempotentReplay)
            {
                context.Response.Headers["Idempotent-Replay"] = "true";
                EventAvailabilityTelemetry.Record(activity, "replayed");
                return Results.Ok(responseBody);
            }

            EventAvailabilityTelemetry.Record(activity, result.IsNew ? "created" : "updated");
            return result.IsNew
                ? Results.Created($"/api/events/{parsedEventId}/availability/me", responseBody)
                : Results.Ok(responseBody);
        }
        catch (KeyNotFoundException)
        {
            EventAvailabilityTelemetry.Record(activity, "event_not_found");
            return ApiProblemDetailsService.Problem(context, 404, "event_not_found", "The specified event was not found.");
        }
        catch (InvalidEventStatusException ex)
        {
            EventAvailabilityTelemetry.Record(activity, "invalid_event_status");
            return ApiProblemDetailsService.Problem(context, 409, "invalid_event_status", ex.Message);
        }
        catch (AvailabilityWindowClosedException ex)
        {
            EventAvailabilityTelemetry.Record(activity, "window_closed");
            return ApiProblemDetailsService.Problem(context, 409, "window_closed", ex.Message);
        }
        catch (ChiefLeaderExclusivityException ex)
        {
            EventAvailabilityTelemetry.Record(activity, "chief_leader_conflict");
            return ApiProblemDetailsService.Problem(context, 409, "chief_leader_conflict", ex.Message);
        }
        catch (StaleAvailabilityVersionException)
        {
            EventAvailabilityTelemetry.Record(activity, "stale_version");
            return ApiProblemDetailsService.Problem(context, 409, "stale_version", "The availability declaration changed since it was opened. Refresh and review your changes.");
        }
    }

    private static bool TryGetActor(HttpContext context, out Guid userId)
    {
        userId = Guid.Empty;
        var value = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out userId);
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

    public sealed record DeclareAvailabilityRequest(string Response);

    public sealed record MyAvailabilityResponse(
        Guid EventId,
        Guid StaffingCycleId,
        string? Response,
        DateTimeOffset? DeclaredAt,
        string? Version,
        bool IsWindowOpen,
        string EventStatus,
        bool IsChiefLeaderOnEvent);

    public sealed record AvailabilityDeclarationResponse(
        Guid Id,
        Guid EventId,
        Guid StaffingCycleId,
        Guid UserId,
        string Response,
        DateTimeOffset DeclaredAt,
        string Version);
}
