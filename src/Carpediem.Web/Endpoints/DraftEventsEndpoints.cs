using System.Globalization;
using System.Security.Claims;
using Carpediem.Modules.Events.Application.Drafts;
using Carpediem.Modules.Events.Application.EventPublication;
using Carpediem.Modules.Events.Application.Time;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Web.Errors;
using Carpediem.Web.Observability;
using Microsoft.AspNetCore.Antiforgery;

namespace Carpediem.Web.Endpoints;

public static class DraftEventsEndpoints
{
    public static IEndpointRouteBuilder MapDraftEventsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events/drafts")
            .WithTags("Draft Events")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);
        group.MapGet("/", ListAsync)
            .WithName("ListDraftEvents")
            .Produces<DraftListResponse>()
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json");
        group.MapGet("/{id}", GetAsync)
            .WithName("GetDraftEvent")
            .Produces<DraftEventResponse>()
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json");
        group.MapPost("/", CreateAsync)
            .WithName("CreateDraftEvent")
            .Accepts<DraftWriteInput>("application/json")
            .Produces<DraftEventResponse>(201)
            .Produces<DraftEventResponse>(200)
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(422, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        group.MapPut("/{id}", UpdateAsync)
            .WithName("UpdateDraftEvent")
            .Accepts<DraftUpdateRequest>("application/json")
            .Produces<DraftEventResponse>()
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(422, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json")
            .WithMetadata(new RequireAntiforgeryTokenAttribute());
        group.MapPost("/{id}/publish", PublishAsync)
            .WithName("PublishDraftEvent")
            .Produces<PublishedEventResponse>(200)
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

    private static async Task<IResult> ListAsync(
        int? page,
        int? pageSize,
        HttpContext context,
        IDraftEventStore store,
        CancellationToken cancellationToken)
    {
        var selectedPage = page ?? 1;
        var selectedPageSize = pageSize ?? 20;
        if (selectedPage < 1 || selectedPageSize is < 1 or > 100)
        {
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "Page and page size are out of range.");
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId))
        {
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        try
        {
            var drafts = await store.ListAsync(selectedPage, selectedPageSize, actorUserId, cancellationToken);
            var items = drafts.Select(draft => new DraftSummaryResponse(
                draft.Id,
                draft.Title,
                ToCairoTime(CairoEventTimeResolver.FromUtc(draft.StartDateTime)),
                draft.Location,
                draft.Status.ToString()));
            return Results.Ok(new DraftListResponse(items.ToArray(), selectedPage, selectedPageSize));
        }
        catch (UnauthorizedAccessException)
        {
            return ApiProblemDetailsService.Problem(context, 403, "forbidden", "Draft Event access is denied.");
        }
    }

    private static async Task<IResult> GetAsync(
        string id,
        HttpContext context,
        IDraftEventStore store,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var eventId))
        {
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId))
        {
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        try
        {
            var draft = await store.GetAsync(eventId, actorUserId, cancellationToken);
            return Results.Ok(ToResponse(draft,
                CairoEventTimeResolver.FromUtc(draft.StartDateTime),
                CairoEventTimeResolver.FromUtc(draft.EndDateTime)));
        }
        catch (DraftNotFoundException)
        {
            return ApiProblemDetailsService.Problem(context, 404, "event_not_found", "Draft Event was not found.");
        }
        catch (NotDraftException)
        {
            return ApiProblemDetailsService.Problem(context, 409, "not_draft", "The Event is no longer a Draft.");
        }
        catch (UnauthorizedAccessException)
        {
            return ApiProblemDetailsService.Problem(context, 403, "forbidden", "Draft Event access is denied.");
        }
    }

    private static async Task<IResult> CreateAsync(
        DraftWriteInput input,
        HttpContext context,
        IAntiforgery antiforgery,
        CreateDraftEventHandler handler,
        CancellationToken cancellationToken)
    {
        if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false } ||
            !await HasValidAntiforgeryAsync(context, antiforgery))
        {
            return ApiProblemDetailsService.Problem(context, 400, "antiforgery_failed", "The request could not be verified.");
        }

        if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"], out var requestToken) ||
            requestToken == Guid.Empty)
        {
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid creation key is required.");
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId) ||
            actorUserId == Guid.Empty)
        {
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        try
        {
            var correlationId = (Guid)context.Items["CorrelationId"]!;
            var created = await handler.HandleAsync(input, actorUserId, requestToken, correlationId,
                cancellationToken);
            var response = ToResponse(created.Draft, created.Start, created.End);
            if (created.IsReplay)
            {
                context.Response.Headers["Idempotent-Replay"] = "true";
                return Results.Ok(response);
            }

            return Results.Created($"/api/events/drafts/{created.Draft.Id}", response);
        }
        catch (DraftValidationException error)
        {
            return ApiProblemDetailsService.Problem(context, 422, "validation_failed", "Review the highlighted fields.",
                errors: error.Errors);
        }
        catch (CairoTimeValidationException error)
        {
            var extensions = error.AllowedOffsets.Count > 0
                ? new Dictionary<string, object?> { ["allowedOffsets"] = error.AllowedOffsets }
                : null;
            return ApiProblemDetailsService.Problem(context, 422, "validation_failed", "Review the highlighted fields.",
                errors: error.Errors, additionalExtensions: extensions);
        }
        catch (DraftCreateConflictException)
        {
            return ApiProblemDetailsService.Problem(context, 409, "idempotency_key_reused", "This creation key has already been used with different values.");
        }
        catch (UnauthorizedAccessException)
        {
            return ApiProblemDetailsService.Problem(context, 403, "forbidden", "Draft Event access is denied.");
        }
    }

    private static async Task<IResult> UpdateAsync(
        string id,
        DraftUpdateRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        UpdateDraftEventHandler handler,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(id, out var eventId))
        {
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
        }

        if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false } ||
            !await HasValidAntiforgeryAsync(context, antiforgery))
        {
            return ApiProblemDetailsService.Problem(context, 400, "antiforgery_failed", "The request could not be verified.");
        }

        byte[] expectedVersion;
        try
        {
            expectedVersion = Convert.FromBase64String(request.Version ?? string.Empty);
            if (expectedVersion.Length != 8) throw new FormatException("Invalid version length.");
        }
        catch (FormatException)
        {
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Draft version is required.");
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId))
        {
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        try
        {
            var input = new DraftWriteInput(request.Title, request.Start, request.End,
                request.Location, request.Notes);
            var correlationId = (Guid)context.Items["CorrelationId"]!;
            var updated = await handler.HandleAsync(eventId, input, expectedVersion, actorUserId,
                correlationId, cancellationToken);
            return Results.Ok(ToResponse(updated.Draft, updated.Start, updated.End));
        }
        catch (DraftNotFoundException)
        {
            return ApiProblemDetailsService.Problem(context, 404, "event_not_found", "Draft Event was not found.");
        }
        catch (NotDraftException)
        {
            return ApiProblemDetailsService.Problem(context, 409, "not_draft", "The Event is no longer a Draft.");
        }
        catch (StaleDraftException)
        {
            return ApiProblemDetailsService.Problem(context, 409, "stale_draft", "The Draft changed since it was opened. Refresh and review your changes.");
        }
        catch (DraftValidationException error)
        {
            return ApiProblemDetailsService.Problem(context, 422, "validation_failed", "Review the highlighted fields.",
                errors: error.Errors);
        }
        catch (CairoTimeValidationException error)
        {
            var extensions = error.AllowedOffsets.Count > 0
                ? new Dictionary<string, object?> { ["allowedOffsets"] = error.AllowedOffsets }
                : null;
            return ApiProblemDetailsService.Problem(context, 422, "validation_failed", "Review the highlighted fields.",
                errors: error.Errors, additionalExtensions: extensions);
        }
        catch (UnauthorizedAccessException)
        {
            return ApiProblemDetailsService.Problem(context, 403, "forbidden", "Draft Event access is denied.");
        }
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

    private static DraftEventResponse ToResponse(Event draft, CairoResolvedTime start, CairoResolvedTime end)
    {
        return new DraftEventResponse(
            draft.Id,
            draft.Title,
            ToCairoTime(start),
            ToCairoTime(end),
            FormatUtc(draft.StartDateTime),
            FormatUtc(draft.EndDateTime),
            draft.Location,
            draft.Notes,
            draft.Status.ToString(),
            Convert.ToBase64String(draft.RowVersion));
    }

    private static string FormatUtc(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", CultureInfo.InvariantCulture);

    private static CairoTimeResponse ToCairoTime(CairoResolvedTime value) =>
        new(value.LocalDateTime, CairoEventTimeResolver.FormatOffset(value.Offset));

    private sealed record CairoTimeResponse(string LocalDateTime, string Offset);
    private sealed record DraftSummaryResponse(Guid Id, string Title, CairoTimeResponse Start, string Location, string Status);
    private sealed record DraftListResponse(DraftSummaryResponse[] Items, int Page, int PageSize);
    private sealed record DraftUpdateRequest(
        string? Title,
        CairoTimeInput? Start,
        CairoTimeInput? End,
        string? Location,
        string? Notes,
        string? Version);
    private sealed record DraftEventResponse(
        Guid Id,
        string Title,
        CairoTimeResponse Start,
        CairoTimeResponse End,
        string StartDateTimeUtc,
        string EndDateTimeUtc,
        string Location,
        string? Notes,
        string Status,
        string Version);

    private static async Task<IResult> PublishAsync(
        string id,
        HttpContext context,
        IAntiforgery antiforgery,
        IEventPublicationStore store,
        CancellationToken cancellationToken)
    {
        using var activity = EventPublicationTelemetry.Start();

        if (!Guid.TryParse(id, out var eventId))
        {
            EventPublicationTelemetry.Record(activity, "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid Event identifier is required.");
        }

        if (context.Features.Get<IAntiforgeryValidationFeature>() is { IsValid: false } ||
            !await HasValidAntiforgeryAsync(context, antiforgery))
        {
            EventPublicationTelemetry.Record(activity, "antiforgery_failed");
            return ApiProblemDetailsService.Problem(context, 400, "antiforgery_failed", "The request could not be verified.");
        }

        var ifMatch = context.Request.Headers.IfMatch.ToString();
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            EventPublicationTelemetry.Record(activity, "missing_if_match");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "If-Match header is required.");
        }

        var cleanVersion = ifMatch.Trim().Trim('"');
        byte[] expectedVersion;
        try
        {
            expectedVersion = Convert.FromBase64String(cleanVersion);
            if (expectedVersion.Length != 8) throw new FormatException("Invalid version length.");
        }
        catch (FormatException)
        {
            EventPublicationTelemetry.Record(activity, "invalid_if_match");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request", "A valid version is required in the If-Match header.");
        }

        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorUserId))
        {
            EventPublicationTelemetry.Record(activity, "unauthenticated");
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated", "The session is no longer active.");
        }

        try
        {
            var correlationId = context.Items.TryGetValue("CorrelationId", out var item) && item is Guid guid
                ? guid
                : Guid.NewGuid();

            var published = await store.PublishAsync(
                eventId,
                actorUserId,
                expectedVersion,
                correlationId,
                DateTimeOffset.UtcNow,
                cancellationToken);

            if (published.IsReplay)
            {
                context.Response.Headers["Idempotent-Replay"] = "true";
                EventPublicationTelemetry.Record(activity, "replayed");
            }
            else
            {
                EventPublicationTelemetry.Record(activity, "published");
            }

            return Results.Ok(new PublishedEventResponse(
                published.Id,
                published.Title,
                FormatUtc(published.StartDateTime),
                FormatUtc(published.EndDateTime),
                published.Location,
                published.Notes,
                published.Status.ToString(),
                new ActiveChiefLeaderResponse(
                    published.ActiveChiefLeader.UserId,
                    published.ActiveChiefLeader.UserName,
                    published.ActiveChiefLeader.AssignmentId),
                Convert.ToBase64String(published.Version),
                FormatUtc(published.PublishedAt)));
        }
        catch (EventPublicationNotFoundException)
        {
            EventPublicationTelemetry.Record(activity, "not_found");
            return ApiProblemDetailsService.Problem(context, 404, "event_not_found", "Draft Event was not found.");
        }
        catch (NotDraftException)
        {
            EventPublicationTelemetry.Record(activity, "not_draft");
            return ApiProblemDetailsService.Problem(context, 409, "not_draft", "The Event is no longer a Draft.");
        }
        catch (EventPublicationConflictException error)
        {
            EventPublicationTelemetry.Record(activity, "conflict");
            return ApiProblemDetailsService.Problem(context, 409, "conflict", error.Message);
        }
        catch (ActiveChiefLeaderRequiredException error)
        {
            EventPublicationTelemetry.Record(activity, "active_chief_leader_required");
            return ApiProblemDetailsService.Problem(context, 422, "active_chief_leader_required", error.Message);
        }
        catch (EventPublicationValidationException error)
        {
            EventPublicationTelemetry.Record(activity, "validation_failed");
            return ApiProblemDetailsService.Problem(context, 422, "validation_failed", error.Message);
        }
        catch (UnauthorizedAccessException)
        {
            EventPublicationTelemetry.Record(activity, "forbidden");
            return ApiProblemDetailsService.Problem(context, 403, "forbidden", "Draft Event access is denied.");
        }
    }

    public sealed record ActiveChiefLeaderResponse(Guid UserId, string UserName, Guid AssignmentId);
    public sealed record PublishedEventResponse(
        Guid Id,
        string Title,
        string StartDateTime,
        string EndDateTime,
        string Location,
        string? Notes,
        string Status,
        ActiveChiefLeaderResponse ActiveChiefLeader,
        string Version,
        string PublishedAt);
}
