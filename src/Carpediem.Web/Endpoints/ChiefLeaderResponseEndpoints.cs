using System.Security.Claims;
using Carpediem.Modules.Events.Application.ChiefLeaderResponses;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Web.Errors;
using Microsoft.AspNetCore.Antiforgery;

namespace Carpediem.Web.Endpoints;

public static class ChiefLeaderResponseEndpoints
{
    public static IEndpointRouteBuilder MapChiefLeaderResponseEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/chief-leader/appointments")
            .WithTags("Chief Leader Direct Response")
            .RequireAuthorization(AuthorizationPolicies.RequireChiefLeader);

        group.MapGet("/", ListAsync).WithName("ListMyPendingChiefLeaderAppointments")
            .Produces<ResponseView[]>(200)
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json");
        group.MapGet("/{assignmentId}", GetAsync).WithName("GetMyChiefLeaderAppointment")
            .Produces<ResponseView>(200)
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json");
        group.MapPost("/{assignmentId}/accept",
                (string assignmentId, HttpContext context, IAntiforgery antiforgery,
                    IChiefLeaderResponseStore store, CancellationToken cancellationToken) =>
                    RespondAsync(assignmentId, ChiefLeaderDecision.Accept, context,
                        antiforgery, store, cancellationToken))
            .WithName("AcceptMyChiefLeaderAppointment")
            .Produces<ResponseView>(200)
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        group.MapPost("/{assignmentId}/decline",
                (string assignmentId, HttpContext context, IAntiforgery antiforgery,
                    IChiefLeaderResponseStore store, CancellationToken cancellationToken) =>
                    RespondAsync(assignmentId, ChiefLeaderDecision.Decline, context,
                        antiforgery, store, cancellationToken))
            .WithName("DeclineMyChiefLeaderAppointment")
            .Produces<ResponseView>(200)
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        return endpoints;
    }

    private static async Task<IResult> ListAsync(
        HttpContext context, IChiefLeaderResponseStore store, CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId))
            return Problem(context, 401, "unauthenticated", "The session is no longer active.");
        try
        {
            var items = await store.ListPendingAsync(actorUserId, cancellationToken);
            return Results.Ok(items.Select(ToResponse).ToArray());
        }
        catch (UnauthorizedAccessException)
        {
            return Problem(context, 403, "forbidden", "Chief Leader appointment access is denied.");
        }
    }

    private static async Task<IResult> GetAsync(
        string assignmentId, HttpContext context, IChiefLeaderResponseStore store,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(assignmentId, out var id) || id == Guid.Empty)
            return Problem(context, 400, "malformed_request", "A valid appointment identifier is required.");
        if (!TryGetActor(context, out var actorUserId))
            return Problem(context, 401, "unauthenticated", "The session is no longer active.");
        try
        {
            return Results.Ok(ToResponse(await store.GetAsync(id, actorUserId, cancellationToken)));
        }
        catch (ChiefLeaderResponseNotFoundException)
        {
            return Problem(context, 404, "appointment_not_found", "Appointment was not found.");
        }
        catch (UnauthorizedAccessException)
        {
            return Problem(context, 403, "forbidden", "Chief Leader appointment access is denied.");
        }
    }

    private static async Task<IResult> RespondAsync(
        string assignmentId, ChiefLeaderDecision decision, HttpContext context,
        IAntiforgery antiforgery, IChiefLeaderResponseStore store,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(assignmentId, out var id) || id == Guid.Empty)
            return Problem(context, 400, "malformed_request", "A valid appointment identifier is required.");
        if (!await HasValidAntiforgeryAsync(context, antiforgery))
            return Problem(context, 400, "antiforgery_failed", "The request could not be verified.");
        var rawVersion = context.Request.Headers.IfMatch.ToString().Trim('"');
        byte[] version;
        try
        {
            version = Convert.FromBase64String(rawVersion);
            if (version.Length != 8) throw new FormatException();
        }
        catch (FormatException)
        {
            return Problem(context, 400, "malformed_request", "A valid appointment version is required.");
        }
        if (!TryGetActor(context, out var actorUserId))
            return Problem(context, 401, "unauthenticated", "The session is no longer active.");

        try
        {
            var result = await store.RespondAsync(id, actorUserId, version, decision,
                (Guid)context.Items["CorrelationId"]!, DateTimeOffset.UtcNow, cancellationToken);
            return Results.Ok(ToResponse(result));
        }
        catch (ChiefLeaderResponseNotFoundException)
        {
            return Problem(context, 404, "appointment_not_found", "Appointment was not found.");
        }
        catch (Exception error) when (error is ChiefLeaderResponseConflictException or ChiefLeaderResponseStateException)
        {
            return Problem(context, 409, "appointment_conflict",
                "The appointment changed. Review its current state before responding.");
        }
        catch (ActiveTeamAssignmentConflictException)
        {
            return Problem(context, 409, "team_assignment_conflict",
                "Resolve the active team assignment on this Event before accepting.");
        }
        catch (UnauthorizedAccessException)
        {
            return Problem(context, 403, "forbidden", "Chief Leader appointment access is denied.");
        }
    }

    private static IResult Problem(HttpContext context, int status, string code, string detail) =>
        ApiProblemDetailsService.Problem(context, status, code, detail);

    private static bool TryGetActor(HttpContext context, out Guid actorUserId) =>
        Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out actorUserId) &&
        actorUserId != Guid.Empty;

    private static async Task<bool> HasValidAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false }) return false;
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

    private static ResponseView ToResponse(ChiefLeaderResponseView value) => new(
        value.Id, value.EventId, value.EventTitle, value.StartDateTime.ToUniversalTime(),
        value.EndDateTime.ToUniversalTime(), value.Location, value.Status.ToString(),
        value.AppointedAt.ToUniversalTime(), value.AcceptedAt?.ToUniversalTime(),
        value.DeclinedAt?.ToUniversalTime(), Convert.ToBase64String(value.Version));

    private sealed record ResponseView(Guid Id, Guid EventId, string EventTitle,
        DateTimeOffset StartDateTime, DateTimeOffset EndDateTime, string Location,
        string Status, DateTimeOffset AppointedAt, DateTimeOffset? AcceptedAt,
        DateTimeOffset? DeclinedAt, string Version);
}
