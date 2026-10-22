using System.Security.Claims;
using Carpediem.Modules.Identity.Application;
using Carpediem.Web.Errors;

namespace Carpediem.Web.Endpoints;

public static class LeaderHistoryEndpoints
{
    public static IEndpointRouteBuilder MapLeaderHistoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api")
            .WithTags("Leader History")
            .RequireAuthorization();

        group.MapGet("/profile/me", GetOwnAsync)
            .WithName("GetOwnLeaderHistory")
            .Produces<LeaderHistoryResult>(200)
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json");

        group.MapGet("/leaders/{leaderId}/history", GetLeaderAsync)
            .WithName("GetLeaderHistory")
            .Produces<LeaderHistoryResult>(200)
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json");

        return endpoints;
    }

    private static async Task<IResult> GetOwnAsync(
        HttpContext context,
        ILeaderHistoryStore store,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(context, out var actorUserId))
        {
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        try
        {
            var result = await store.GetOwnAsync(actorUserId, cancellationToken);
            if (result is not null) context.Response.Headers.CacheControl = "no-store";
            return result is null
                ? ApiProblemDetailsService.Problem(context, 404, "leader_not_found", "The Leader was not found.")
                : Results.Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return ApiProblemDetailsService.Problem(context, 403, "leader_history_forbidden", "Leader history is not available in this context.");
        }
    }

    private static async Task<IResult> GetLeaderAsync(
        string leaderId,
        string? eventId,
        HttpContext context,
        ILeaderHistoryStore store,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(leaderId, out var parsedLeaderId))
        {
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Leader identifier is required.");
        }

        Guid? parsedEventId = null;
        if (eventId is not null)
        {
            if (!Guid.TryParse(eventId, out var id))
            {
                return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
            }
            parsedEventId = id;
        }

        if (!TryGetActor(context, out var actorUserId))
        {
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        try
        {
            var result = await store.GetLeaderAsync(actorUserId, parsedLeaderId, parsedEventId, cancellationToken);
            if (result is not null) context.Response.Headers.CacheControl = "no-store";
            return result is null
                ? ApiProblemDetailsService.Problem(context, 404, "leader_not_found", "The Leader was not found in this context.")
                : Results.Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return ApiProblemDetailsService.Problem(context, 403, "leader_history_forbidden", "Leader history is not available in this context.");
        }
    }

    private static bool TryGetActor(HttpContext context, out Guid actorUserId) =>
        Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out actorUserId);
}
