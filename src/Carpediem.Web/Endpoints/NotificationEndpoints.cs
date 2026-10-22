using System.Security.Claims;
using Carpediem.Modules.Notifications.Application;
using Carpediem.Web.Errors;
using Microsoft.AspNetCore.Antiforgery;

namespace Carpediem.Web.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications")
            .WithTags("Notifications")
            .RequireAuthorization();

        group.MapGet("/", GetLatestAsync).WithName("GetMyNotifications")
            .Produces<IReadOnlyList<NotificationInboxItem>>(200);
        group.MapGet("/unread-count", GetUnreadCountAsync).WithName("GetMyUnreadNotificationCount")
            .Produces<UnreadCountResponse>(200);
        group.MapPost("/{notificationId:guid}/read", MarkReadAsync).WithName("MarkNotificationRead")
            .WithMetadata(new RequireAntiforgeryTokenAttribute())
            .Produces(204).Produces<ApiProblemResponse>(404, "application/problem+json");
        group.MapPost("/mark-all-read", MarkAllReadAsync).WithName("MarkAllNotificationsRead")
            .WithMetadata(new RequireAntiforgeryTokenAttribute())
            .Produces<MarkAllReadResponse>(200);
        return endpoints;
    }

    private static async Task<IResult> GetLatestAsync(
        HttpContext context, INotificationInboxStore store, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(context, out var userId)) return Unauthenticated(context);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(await store.GetLatestAsync(userId, cancellationToken));
    }

    private static async Task<IResult> GetUnreadCountAsync(
        HttpContext context, INotificationInboxStore store, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(context, out var userId)) return Unauthenticated(context);
        context.Response.Headers.CacheControl = "no-store";
        return Results.Ok(new UnreadCountResponse(await store.GetUnreadCountAsync(userId, cancellationToken)));
    }

    private static async Task<IResult> MarkReadAsync(
        Guid notificationId, HttpContext context, IAntiforgery antiforgery,
        INotificationInboxStore store, TimeProvider clock, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!await IsValidAntiforgeryAsync(context, antiforgery)) return AntiforgeryFailed(context);
        if (!TryGetUserId(context, out var userId)) return Unauthenticated(context);
        if (!await store.MarkReadAsync(userId, notificationId, clock.GetUtcNow(), cancellationToken))
            return ApiProblemDetailsService.Problem(context, 404, "notification_not_found", "Notification not found.");
        return Results.NoContent();
    }

    private static async Task<IResult> MarkAllReadAsync(
        HttpContext context, IAntiforgery antiforgery, INotificationInboxStore store,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        context.Response.Headers.CacheControl = "no-store";
        if (!await IsValidAntiforgeryAsync(context, antiforgery)) return AntiforgeryFailed(context);
        if (!TryGetUserId(context, out var userId)) return Unauthenticated(context);
        var updated = await store.MarkAllReadAsync(userId, clock.GetUtcNow(), cancellationToken);
        return Results.Ok(new MarkAllReadResponse(updated));
    }

    private static bool TryGetUserId(HttpContext context, out Guid userId) =>
        Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static async Task<bool> IsValidAntiforgeryAsync(HttpContext context, IAntiforgery antiforgery)
    {
        if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false }) return false;
        try { await antiforgery.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static IResult Unauthenticated(HttpContext context) =>
        ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");

    private static IResult AntiforgeryFailed(HttpContext context) =>
        ApiProblemDetailsService.Problem(context, 400, "antiforgery_failed", "The request could not be verified.");

    private sealed record UnreadCountResponse(int Count);
    private sealed record MarkAllReadResponse(int Updated);
}
