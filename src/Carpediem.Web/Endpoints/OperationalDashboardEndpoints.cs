using System.Security.Claims;
using Carpediem.Modules.Events.Application.Dashboard;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Web.Errors;

namespace Carpediem.Web.Endpoints;

public static class OperationalDashboardEndpoints
{
    public static IEndpointRouteBuilder MapOperationalDashboardEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/dashboard/events", GetDashboardAsync)
            .WithTags("Operational Dashboard")
            .RequireAuthorization(AuthorizationPolicies.RequireChiefLeaderOrAdmin)
            .WithName("GetOperationalDashboard")
            .Produces<IReadOnlyList<OperationalEvent>>(200)
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json");

        return endpoints;
    }

    private static async Task<IResult> GetDashboardAsync(
        HttpContext context,
        IOperationalDashboardStore store,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";

        if (!TryGetActor(context, out var actorUserId))
        {
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        try
        {
            var result = await store.GetAsync(actorUserId, clock.GetUtcNow(), cancellationToken);
            return Results.Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return ApiProblemDetailsService.Problem(context, 403, "dashboard_forbidden", "Only Platform Administrators and Chief Leaders can view the operational dashboard.");
        }
    }

    private static bool TryGetActor(HttpContext context, out Guid actorUserId) =>
        Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out actorUserId);
}
