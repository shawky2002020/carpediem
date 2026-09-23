using System.Security.Claims;
using Carpediem.Modules.Events.Application.ChiefLeaderAppointments;
using Carpediem.Modules.Events.Domain.ChiefLeaderAssignments;
using Carpediem.Modules.Events.Domain.Events;
using Carpediem.Modules.Identity.Authorization;
using Carpediem.Web.Errors;
using Carpediem.Web.Observability;
using Microsoft.AspNetCore.Antiforgery;

namespace Carpediem.Web.Endpoints;

public static class ChiefLeaderAppointmentEndpoints
{
    public static IEndpointRouteBuilder MapChiefLeaderAppointmentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/events/drafts/{eventId}/chief-leader-appointment")
            .WithTags("Chief Leader Appointment")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);
        group.MapGet("/", GetAsync)
            .WithName("GetChiefLeaderAppointment")
            .Produces<AppointmentContextResponse>()
            .Produces<ApiProblemResponse>(400, "application/problem+json")
            .Produces<ApiProblemResponse>(401, "application/problem+json")
            .Produces<ApiProblemResponse>(403, "application/problem+json")
            .Produces<ApiProblemResponse>(404, "application/problem+json")
            .Produces<ApiProblemResponse>(409, "application/problem+json")
            .Produces<ApiProblemResponse>(500, "application/problem+json");
        group.MapPost("/", AppointAsync)
            .WithName("AppointChiefLeader")
            .Accepts<AppointRequest>("application/json")
            .Produces<AppointmentResponse>(201)
            .Produces<AppointmentResponse>(200)
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

    private static async Task<IResult> GetAsync(
        string eventId,
        HttpContext context,
        IChiefLeaderAppointmentStore store,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(eventId, out var selectedEventId))
        {
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request",
                "A valid Event identifier is required.");
        }
        if (!TryGetActor(context, out var actorUserId))
        {
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated",
                "The session is no longer active.");
        }

        try
        {
            var result = await store.GetContextAsync(selectedEventId, actorUserId, cancellationToken);
            return Results.Ok(new AppointmentContextResponse(result.EventId, result.EventTitle,
                result.EventStatus,
                result.CurrentAppointment is null ? null : ToResponse(result.CurrentAppointment),
                result.DeclinedHistory.Select(ToResponse).ToArray(),
                result.Candidates.Select(item => new CandidateResponse(item.UserId, item.UserName)).ToArray()));
        }
        catch (ChiefLeaderAppointmentNotFoundException)
        {
            return ApiProblemDetailsService.Problem(context, 404, "event_not_found", "Draft Event was not found.");
        }
        catch (NotDraftException)
        {
            return ApiProblemDetailsService.Problem(context, 409, "not_draft", "The Event is no longer a Draft.");
        }
        catch (UnauthorizedAccessException)
        {
            return ApiProblemDetailsService.Problem(context, 403, "forbidden", "Chief Leader appointment access is denied.");
        }
    }

    private static async Task<IResult> AppointAsync(
        string eventId,
        AppointRequest request,
        HttpContext context,
        IAntiforgery antiforgery,
        AppointChiefLeaderHandler handler,
        CancellationToken cancellationToken)
    {
        using var activity = ChiefLeaderAppointmentTelemetry.Start();
        if (!Guid.TryParse(eventId, out var selectedEventId) ||
            !Guid.TryParse(request.CandidateUserId, out var candidateUserId) || candidateUserId == Guid.Empty)
        {
            ChiefLeaderAppointmentTelemetry.Record(activity, "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request",
                "Valid Event and candidate identifiers are required.");
        }
        if (!await HasValidAntiforgeryAsync(context, antiforgery))
        {
            ChiefLeaderAppointmentTelemetry.Record(activity, "antiforgery_failed");
            return ApiProblemDetailsService.Problem(context, 400, "antiforgery_failed",
                "The request could not be verified.");
        }
        if (!Guid.TryParse(context.Request.Headers["Idempotency-Key"], out var requestToken) ||
            requestToken == Guid.Empty)
        {
            ChiefLeaderAppointmentTelemetry.Record(activity, "malformed_request");
            return ApiProblemDetailsService.Problem(context, 400, "malformed_request",
                "A valid appointment key is required.");
        }
        if (!TryGetActor(context, out var actorUserId))
        {
            ChiefLeaderAppointmentTelemetry.Record(activity, "unauthenticated");
            return ApiProblemDetailsService.Problem(context, 401, "unauthenticated",
                "The session is no longer active.");
        }

        try
        {
            var correlationId = (Guid)context.Items["CorrelationId"]!;
            var result = await handler.HandleAsync(selectedEventId, candidateUserId, actorUserId,
                requestToken, correlationId, cancellationToken);
            var response = ToResponse(result.Appointment);
            if (result.IsReplay)
            {
                ChiefLeaderAppointmentTelemetry.Record(activity, "replayed");
                context.Response.Headers["Idempotent-Replay"] = "true";
                return Results.Ok(response);
            }
            ChiefLeaderAppointmentTelemetry.Record(activity, "created");
            return Results.Created($"/api/events/drafts/{selectedEventId}/chief-leader-appointment", response);
        }
        catch (ChiefLeaderAppointmentNotFoundException)
        {
            ChiefLeaderAppointmentTelemetry.Record(activity, "event_not_found");
            return ApiProblemDetailsService.Problem(context, 404, "event_not_found", "Draft Event was not found.");
        }
        catch (NotDraftException)
        {
            ChiefLeaderAppointmentTelemetry.Record(activity, "not_draft");
            return ApiProblemDetailsService.Problem(context, 409, "not_draft", "The Event is no longer a Draft.");
        }
        catch (ChiefLeaderAlreadyAssignedException)
        {
            ChiefLeaderAppointmentTelemetry.Record(activity, "chief_leader_already_assigned");
            return ApiProblemDetailsService.Problem(context, 409, "chief_leader_already_assigned",
                "This Event already has an unresolved Chief Leader appointment.");
        }
        catch (AppointmentConflictException)
        {
            ChiefLeaderAppointmentTelemetry.Record(activity, "appointment_conflict");
            return ApiProblemDetailsService.Problem(context, 409, "appointment_conflict",
                "The appointment changed while this request was being saved. Review the current appointment.");
        }
        catch (AppointmentRequestConflictException)
        {
            ChiefLeaderAppointmentTelemetry.Record(activity, "idempotency_key_reused");
            return ApiProblemDetailsService.Problem(context, 409, "idempotency_key_reused",
                "This appointment key has already been used with different values.");
        }
        catch (CandidateIneligibleException)
        {
            ChiefLeaderAppointmentTelemetry.Record(activity, "candidate_ineligible");
            return ApiProblemDetailsService.Problem(context, 422, "candidate_ineligible",
                "Review the selected candidate.", errors: new Dictionary<string, string[]>
                {
                    ["candidateUserId"] = ["The selected Chief Leader is no longer eligible."],
                });
        }
        catch (UnauthorizedAccessException)
        {
            ChiefLeaderAppointmentTelemetry.Record(activity, "forbidden");
            return ApiProblemDetailsService.Problem(context, 403, "forbidden", "Chief Leader appointment access is denied.");
        }
        catch
        {
            ChiefLeaderAppointmentTelemetry.Record(activity, "unexpected_error");
            throw;
        }
    }

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

    private static AppointmentResponse ToResponse(ChiefLeaderAppointmentView value) => new(
        value.Id,
        value.EventId,
        new CandidateResponse(value.Candidate.UserId, value.Candidate.UserName),
        value.Status.ToString(),
        value.AppointedAt.ToUniversalTime(),
        value.AppointedByUserId,
        Convert.ToBase64String(value.Version));

    private sealed record AppointRequest(string CandidateUserId);
    private sealed record CandidateResponse(Guid UserId, string UserName);
    private sealed record AppointmentResponse(Guid Id, Guid EventId, CandidateResponse Candidate,
        string Status, DateTimeOffset AppointedAt, Guid AppointedByUserId, string Version);
    private sealed record AppointmentContextResponse(Guid EventId, string EventTitle, string EventStatus,
        AppointmentResponse? CurrentAppointment, AppointmentResponse[] DeclinedHistory,
        CandidateResponse[] Candidates);
}
