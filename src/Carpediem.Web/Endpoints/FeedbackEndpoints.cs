using Carpediem.Infrastructure.Engagement;
using Carpediem.Infrastructure.Audit;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Modules.Staffing.Domain.TeamSelection;
using Carpediem.Web.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;

namespace Carpediem.Web.Endpoints;

public static class FeedbackEndpoints
{
    public static IEndpointRouteBuilder MapFeedbackEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events/{eventId:guid}/feedback").RequireAuthorization().WithTags("Feedback");
        group.MapGet("/context", ContextAsync);
        group.MapPost("/event", SubmitEventAsync).WithMetadata(new RequireAntiforgeryTokenAttribute());
        group.MapPost("/leaders/{leaderId:guid}", SubmitLeaderAsync).WithMetadata(new RequireAntiforgeryTokenAttribute());
        endpoints.MapGet("/api/admin/feedback", AdminListAsync)
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin).WithTags("Feedback");
        return endpoints;
    }

    private static async Task<IResult> ContextAsync(Guid eventId, HttpContext context,
        CarpediemDbContext db, CancellationToken cancellationToken)
    {
        if (!EngagementAccess.Actor(context, out var actorId)) return Problem(context, 401, "unauthenticated", "Sign in is required.");
        context.Response.Headers.CacheControl = "no-store";
        var eventItem = await db.Events.AsNoTracking().FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken);
        if (eventItem is null) return Problem(context, 404, "event_not_found", "Event not found.");
        var completed = eventItem.Status == EventStatus.Completed;
        var isAssigned = context.User.IsInRole(CarpediemRoles.Leader) &&
            await EngagementAccess.AssignedAsync(db, eventId, actorId, cancellationToken);
        var isChief = context.User.IsInRole(CarpediemRoles.ChiefLeader) &&
            await EngagementAccess.LeadsAsync(db, eventId, actorId, cancellationToken);
        var canRateEvent = completed && isAssigned;
        var canRateLeaders = completed && isChief;
        if (!isAssigned && !isChief && !EngagementAccess.IsAdmin(context))
            return Problem(context, 403, "feedback_forbidden", "Feedback is outside your event scope.");
        var leaders = canRateLeaders
            ? await (from assignment in db.TeamAssignments.AsNoTracking()
                     join cycle in db.StaffingCycles.AsNoTracking() on assignment.StaffingCycleId equals cycle.Id
                     join user in db.Users.AsNoTracking() on assignment.UserId equals user.Id
                     where assignment.EventId == eventId && cycle.IsCurrent && assignment.Status == TeamAssignmentStatus.Active
                     orderby user.UserName
                     select new LeaderOption(user.Id, user.UserName ?? string.Empty)).ToListAsync(cancellationToken)
            : [];
        var ownFeedback = await db.EventFeedback.AsNoTracking()
            .Where(item => item.EventId == eventId && item.RaterUserId == actorId)
            .Select(item => new FeedbackSummary(item.Id, item.Kind, item.LeaderUserId, item.Rating, item.Comment))
            .ToListAsync(cancellationToken);
        return Results.Ok(new FeedbackContext(eventItem.Id, eventItem.Title, canRateEvent, canRateLeaders, leaders, ownFeedback));
    }

    private static Task<IResult> SubmitEventAsync(Guid eventId, RatingRequest request,
        HttpContext context, IAntiforgery antiforgery, CarpediemDbContext db,
        TimeProvider clock, CancellationToken cancellationToken) =>
        SubmitAsync(eventId, null, request, context, antiforgery, db, clock, cancellationToken);

    private static Task<IResult> SubmitLeaderAsync(Guid eventId, Guid leaderId, RatingRequest request,
        HttpContext context, IAntiforgery antiforgery, CarpediemDbContext db,
        TimeProvider clock, CancellationToken cancellationToken) =>
        SubmitAsync(eventId, leaderId, request, context, antiforgery, db, clock, cancellationToken);

    private static async Task<IResult> SubmitAsync(Guid eventId, Guid? leaderId, RatingRequest request,
        HttpContext context, IAntiforgery antiforgery, CarpediemDbContext db,
        TimeProvider clock, CancellationToken cancellationToken)
    {
        if (!await VerifyAsync(context, antiforgery)) return Problem(context, 400, "antiforgery_failed", "The request could not be verified.");
        if (!EngagementAccess.Actor(context, out var actorId)) return Problem(context, 401, "unauthenticated", "Sign in is required.");
        if (request is null || request.Rating is < 1 or > 5 || request.Comment?.Trim().Length > 500)
            return Problem(context, 400, "invalid_feedback", "Give a rating from 1 to 5 and an optional comment up to 500 characters.");
        var eventItem = await db.Events.AsNoTracking().FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken);
        if (eventItem is null) return Problem(context, 404, "event_not_found", "Event not found.");
        if (eventItem.Status != EventStatus.Completed)
            return Problem(context, 409, "event_not_completed", "Feedback opens after the Event is completed.");
        var permitted = leaderId.HasValue
            ? context.User.IsInRole(CarpediemRoles.ChiefLeader) &&
              await EngagementAccess.LeadsAsync(db, eventId, actorId, cancellationToken) &&
              await EngagementAccess.AssignedAsync(db, eventId, leaderId.Value, cancellationToken)
            : context.User.IsInRole(CarpediemRoles.Leader) &&
              await EngagementAccess.AssignedAsync(db, eventId, actorId, cancellationToken);
        if (!permitted) return Problem(context, 403, "feedback_forbidden", "You cannot rate this Event or Leader.");
        if (await db.EventFeedback.AnyAsync(item => item.EventId == eventId && item.RaterUserId == actorId &&
            item.LeaderUserId == leaderId, cancellationToken))
            return Problem(context, 409, "feedback_already_submitted", "You have already submitted this rating.");
        var feedback = EventFeedback.Create(eventId, actorId, leaderId, request.Rating, request.Comment, clock.GetUtcNow());
        db.EventFeedback.Add(feedback);
        db.AuditLogs.Add(AuditLog.FeedbackSubmitted(feedback.Id, actorId, feedback.SubmittedAt,
            feedback.Kind, feedback.Rating));
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        { return Problem(context, 409, "feedback_already_submitted", "You have already submitted this rating."); }
        return Results.Created($"/api/events/{eventId}/feedback/context", new FeedbackSummary(feedback.Id,
            feedback.Kind, feedback.LeaderUserId, feedback.Rating, feedback.Comment));
    }

    private static async Task<IResult> AdminListAsync(Guid? eventId, int? page, CarpediemDbContext db,
        HttpContext context, CancellationToken cancellationToken)
    {
        const int pageSize = 50;
        var pageNumber = page ?? 1;
        if (pageNumber is < 1 or > 10000)
            return Problem(context, 400, "invalid_page", "Page must be between 1 and 10000.");
        context.Response.Headers.CacheControl = "no-store";
        var query = db.EventFeedback.AsNoTracking().AsQueryable();
        if (eventId.HasValue) query = query.Where(item => item.EventId == eventId.Value);
        var rows = await (from item in query
                          join eventItem in db.Events.AsNoTracking() on item.EventId equals eventItem.Id
                          join rater in db.Users.AsNoTracking() on item.RaterUserId equals rater.Id
                          join leader in db.Users.AsNoTracking() on item.LeaderUserId equals leader.Id into leaders
                          from leader in leaders.DefaultIfEmpty()
                          orderby item.SubmittedAt descending, item.Id descending
                          select new AdminFeedback(item.Id, item.EventId, eventItem.Title,
                              item.Kind, rater.UserName ?? string.Empty, leader == null ? null : leader.UserName,
                              item.Rating, item.Comment, item.SubmittedAt))
            .Skip((pageNumber - 1) * pageSize).Take(pageSize + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(pageSize);
        return Results.Ok(new AdminFeedbackPage(rows, pageNumber, pageSize, hasMore));
    }

    private static async Task<bool> VerifyAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try { await antiforgery.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
    private static IResult Problem(HttpContext context, int status, string code, string detail) =>
        ApiProblemDetailsService.Problem(context, status, code, detail);
    private sealed record RatingRequest(int Rating, string? Comment);
    private sealed record LeaderOption(Guid UserId, string UserName);
    private sealed record FeedbackSummary(Guid Id, string Kind, Guid? LeaderUserId, int Rating, string? Comment);
    private sealed record FeedbackContext(Guid EventId, string EventTitle, bool CanRateEvent,
        bool CanRateLeaders, IReadOnlyList<LeaderOption> Leaders, IReadOnlyList<FeedbackSummary> OwnFeedback);
    private sealed record AdminFeedback(Guid Id, Guid EventId, string EventTitle, string Kind,
        string RaterUserName, string? LeaderUserName, int Rating, string? Comment, DateTimeOffset SubmittedAt);
    private sealed record AdminFeedbackPage(IReadOnlyList<AdminFeedback> Items, int Page, int PageSize, bool HasMore);
}
