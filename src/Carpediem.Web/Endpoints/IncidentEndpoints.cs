using Carpediem.Infrastructure.Audit;
using Carpediem.Infrastructure.Engagement;
using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Web.Errors;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.EntityFrameworkCore;

namespace Carpediem.Web.Endpoints;

public static class IncidentEndpoints
{
    public static IEndpointRouteBuilder MapIncidentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var events = endpoints.MapGroup("/api/events/{eventId:guid}/incidents").RequireAuthorization().WithTags("Incidents");
        events.MapGet("/", EventListAsync);
        events.MapPost("/", ReportAsync).WithMetadata(new RequireAntiforgeryTokenAttribute());
        var admin = endpoints.MapGroup("/api/admin/incidents")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin).WithTags("Incidents");
        admin.MapGet("/", AdminListAsync);
        admin.MapPost("/{incidentId:guid}/review", ReviewAsync)
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        return endpoints;
    }

    private static async Task<IResult> EventListAsync(Guid eventId, HttpContext context,
        CarpediemDbContext db, CancellationToken cancellationToken)
    {
        if (!EngagementAccess.Actor(context, out var actorId)) return Problem(context, 401, "unauthenticated", "Sign in is required.");
        if (!await db.Events.AnyAsync(item => item.Id == eventId, cancellationToken))
            return Problem(context, 404, "event_not_found", "Event not found.");
        var admin = EngagementAccess.IsAdmin(context);
        var chief = context.User.IsInRole(CarpediemRoles.ChiefLeader) &&
            await EngagementAccess.LeadsAsync(db, eventId, actorId, cancellationToken);
        var leader = context.User.IsInRole(CarpediemRoles.Leader) &&
            await EngagementAccess.AssignedAsync(db, eventId, actorId, cancellationToken);
        if (!admin && !chief && !leader) return Problem(context, 403, "incident_forbidden", "Incidents are outside your event scope.");
        context.Response.Headers.CacheControl = "no-store";
        var query = db.IncidentReports.AsNoTracking().Where(item => item.EventId == eventId);
        if (!admin && !chief) query = query.Where(item => item.ReporterUserId == actorId);
        var incidents = await query.OrderByDescending(item => item.ReportedAt)
            .Select(item => new IncidentSummary(item.Id, item.EventId, item.ReporterUserId,
                item.Title, item.Description, item.OccurredAt, item.ReportedAt,
                item.ReviewedAt, item.ReviewNote)).ToListAsync(cancellationToken);
        return Results.Ok(incidents);
    }

    private static async Task<IResult> ReportAsync(Guid eventId, ReportRequest request, HttpContext context,
        IAntiforgery antiforgery, CarpediemDbContext db, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!await VerifyAsync(context, antiforgery)) return Problem(context, 400, "antiforgery_failed", "The request could not be verified.");
        if (!EngagementAccess.Actor(context, out var actorId)) return Problem(context, 401, "unauthenticated", "Sign in is required.");
        var eventItem = await db.Events.AsNoTracking().FirstOrDefaultAsync(item => item.Id == eventId, cancellationToken);
        if (eventItem is null) return Problem(context, 404, "event_not_found", "Event not found.");
        if (eventItem.Status is EventStatus.Draft or EventStatus.Cancelled)
            return Problem(context, 409, "event_not_reportable", "Incidents can be reported for Published or Completed Events.");
        var permitted = EngagementAccess.IsAdmin(context) ||
            context.User.IsInRole(CarpediemRoles.ChiefLeader) && await EngagementAccess.LeadsAsync(db, eventId, actorId, cancellationToken) ||
            context.User.IsInRole(CarpediemRoles.Leader) && await EngagementAccess.AssignedAsync(db, eventId, actorId, cancellationToken);
        if (!permitted) return Problem(context, 403, "incident_forbidden", "You cannot report an incident for this Event.");
        if (request is null || string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200 ||
            string.IsNullOrWhiteSpace(request.Description) || request.Description.Trim().Length > 4000)
            return Problem(context, 400, "invalid_incident", "Title and description are required within their length limits.");
        var now = clock.GetUtcNow();
        var occurredAt = request.OccurredAt ?? now;
        if (occurredAt.Offset != TimeSpan.Zero || occurredAt > now)
            return Problem(context, 400, "invalid_occurrence_time", "Occurrence time must be UTC and cannot be in the future.");
        var incident = IncidentReport.Create(eventId, actorId, request.Title, request.Description, occurredAt, now);
        db.IncidentReports.Add(incident);
        db.AuditLogs.Add(AuditLog.IncidentReported(incident.Id, actorId, now));
        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/events/{eventId}/incidents", new IncidentSummary(incident.Id,
            eventId, actorId, incident.Title, incident.Description, incident.OccurredAt,
            incident.ReportedAt, null, null));
    }

    private static async Task<IResult> AdminListAsync(int? page, HttpContext context,
        CarpediemDbContext db, CancellationToken cancellationToken)
    {
        const int pageSize = 50;
        var pageNumber = page ?? 1;
        if (pageNumber is < 1 or > 10000)
            return Problem(context, 400, "invalid_page", "Page must be between 1 and 10000.");
        context.Response.Headers.CacheControl = "no-store";
        var rows = await (from item in db.IncidentReports.AsNoTracking()
                          join eventItem in db.Events.AsNoTracking() on item.EventId equals eventItem.Id
                          join reporter in db.Users.AsNoTracking() on item.ReporterUserId equals reporter.Id
                          orderby item.ReportedAt descending, item.Id descending
                          select new AdminIncident(item.Id, item.EventId, eventItem.Title,
                              reporter.UserName ?? string.Empty, item.Title, item.Description,
                              item.OccurredAt, item.ReportedAt, item.ReviewedAt, item.ReviewNote))
            .Skip((pageNumber - 1) * pageSize).Take(pageSize + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > pageSize;
        if (hasMore) rows.RemoveAt(pageSize);
        return Results.Ok(new AdminIncidentPage(rows, pageNumber, pageSize, hasMore));
    }

    private static async Task<IResult> ReviewAsync(Guid incidentId, ReviewRequest request, HttpContext context,
        IAntiforgery antiforgery, CarpediemDbContext db, TimeProvider clock,
        CancellationToken cancellationToken)
    {
        if (!await VerifyAsync(context, antiforgery)) return Problem(context, 400, "antiforgery_failed", "The request could not be verified.");
        if (!EngagementAccess.Actor(context, out var adminId)) return Problem(context, 401, "unauthenticated", "Sign in is required.");
        if (request?.Note?.Trim().Length > 1000)
            return Problem(context, 400, "invalid_review_note", "Review note cannot exceed 1000 characters.");
        var incident = await db.IncidentReports.FirstOrDefaultAsync(item => item.Id == incidentId, cancellationToken);
        if (incident is null) return Problem(context, 404, "incident_not_found", "Incident not found.");
        if (incident.ReviewedAt.HasValue) return Problem(context, 409, "incident_already_reviewed", "Incident has already been reviewed.");
        var now = clock.GetUtcNow();
        incident.Review(adminId, request?.Note, now);
        db.AuditLogs.Add(AuditLog.IncidentReviewed(incident.Id, adminId, now));
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException)
        { return Problem(context, 409, "incident_already_reviewed", "Incident has already been reviewed."); }
        return Results.Ok(new { incident.Id, incident.ReviewedAt, incident.ReviewNote });
    }

    private static async Task<bool> VerifyAsync(HttpContext context, IAntiforgery antiforgery)
    {
        try { await antiforgery.ValidateRequestAsync(context); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }
    private static IResult Problem(HttpContext context, int status, string code, string detail) =>
        ApiProblemDetailsService.Problem(context, status, code, detail);
    private sealed record ReportRequest(string Title, string Description, DateTimeOffset? OccurredAt);
    private sealed record ReviewRequest(string? Note);
    private sealed record IncidentSummary(Guid Id, Guid EventId, Guid ReporterUserId,
        string Title, string Description, DateTimeOffset OccurredAt, DateTimeOffset ReportedAt,
        DateTimeOffset? ReviewedAt, string? ReviewNote);
    private sealed record AdminIncident(Guid Id, Guid EventId, string EventTitle,
        string ReporterUserName, string Title, string Description, DateTimeOffset OccurredAt,
        DateTimeOffset ReportedAt, DateTimeOffset? ReviewedAt, string? ReviewNote);
    private sealed record AdminIncidentPage(IReadOnlyList<AdminIncident> Items, int Page, int PageSize, bool HasMore);
}
