using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events;
using Carpediem.Modules.Identity;
using Carpediem.Modules.Identity.Infrastructure;
using Carpediem.Modules.Staffing;
using Carpediem.Web.Endpoints;
using Carpediem.Web.Errors;
using Carpediem.Web.Observability;
using Carpediem.Web.Security;
using Serilog;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.AddCarpediemObservability();
builder.Services.AddProblemDetails();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, AuthorizationProblemResultHandler>();
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["cookieAuth"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = "Carpediem.Session",
            Description = "Authenticated Carpediem session cookie.",
        };
        return Task.CompletedTask;
    });
    options.AddOperationTransformer((operation, context, _) =>
    {
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAuthorizeData>().Any())
        {
            operation.Security ??= [];
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("cookieAuth", context.Document, null)] = [],
            });
        }
        if (string.Equals(operation.OperationId, "AppointChiefLeader", StringComparison.Ordinal))
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "Idempotency-Key",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Stable request token reused only when retrying the same appointment intent.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-XSRF-TOKEN",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Antiforgery request token paired with the session cookie.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
            if (operation.Responses?.TryGetValue("200", out var replayResponse) == true &&
                replayResponse is OpenApiResponse concreteReplayResponse)
            {
                concreteReplayResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
                concreteReplayResponse.Headers["Idempotent-Replay"] = new OpenApiHeader
                {
                    Description = "True when the response replays the original committed appointment.",
                    Required = true,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                };
            }
        }
        if (operation.OperationId is "AcceptMyChiefLeaderAppointment" or "DeclineMyChiefLeaderAppointment")
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "If-Match",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Opaque assignment version returned by the appointment read.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-XSRF-TOKEN",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Antiforgery request token paired with the session cookie.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
        }
        if (operation.OperationId == "RecordExternalChiefLeaderAcceptance")
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "If-Match",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Opaque assignment version returned by the Admin appointment context.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-XSRF-TOKEN",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Antiforgery request token paired with the session cookie.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
            if (operation.Responses?.TryGetValue("200", out var replayResponse) == true &&
                replayResponse is OpenApiResponse concreteReplayResponse)
            {
                concreteReplayResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
                concreteReplayResponse.Headers["Idempotent-Replay"] = new OpenApiHeader
                {
                    Description = "True when the response replays the committed external acceptance.",
                    Required = false,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                };
            }
        }
        if (operation.OperationId == "PublishDraftEvent")
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "If-Match",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Opaque Event version returned by the Draft Event read.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-XSRF-TOKEN",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Antiforgery request token paired with the session cookie.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
            if (operation.Responses?.TryGetValue("200", out var replayResponse) == true &&
                replayResponse is OpenApiResponse concreteReplayResponse)
            {
                concreteReplayResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
                concreteReplayResponse.Headers["Idempotent-Replay"] = new OpenApiHeader
                {
                    Description = "True when the response replays the committed Event publication.",
                    Required = false,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                };
            }
        }
        if (operation.OperationId == "DeclareAvailability")
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "If-Match",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Base64-encoded rowversion of the existing availability record when updating.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-XSRF-TOKEN",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Antiforgery request token paired with the session cookie.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
            if (operation.Responses?.TryGetValue("200", out var replayResponse) == true &&
                replayResponse is OpenApiResponse concreteReplayResponse)
            {
                concreteReplayResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
                concreteReplayResponse.Headers["Idempotent-Replay"] = new OpenApiHeader
                {
                    Description = "True when the response replays the committed availability declaration.",
                    Required = false,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                };
            }
        }
        if (operation.OperationId == "SelectVolunteer")
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-XSRF-TOKEN",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Antiforgery request token paired with the session cookie.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
            if (operation.Responses?.TryGetValue("200", out var replayResponse) == true &&
                replayResponse is OpenApiResponse concreteReplayResponse)
            {
                concreteReplayResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
                concreteReplayResponse.Headers["Idempotent-Replay"] = new OpenApiHeader
                {
                    Description = "True when the response replays the committed active team assignment.",
                    Required = false,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                };
            }
        }
        if (operation.OperationId == "DirectAssignLeader")
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-XSRF-TOKEN",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Antiforgery request token paired with the session cookie.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
            if (operation.Responses?.TryGetValue("200", out var replayResponse) == true &&
                replayResponse is OpenApiResponse concreteReplayResponse)
            {
                concreteReplayResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
                concreteReplayResponse.Headers["Idempotent-Replay"] = new OpenApiHeader
                {
                    Description = "True when the response replays the committed active team assignment.",
                    Required = false,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                };
            }
        }
        if (operation.OperationId == "CancelAssignment")
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "If-Match",
                In = ParameterLocation.Header,
                Required = false,
                Description = "Base64-encoded rowversion of the existing assignment record for optimistic concurrency.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-XSRF-TOKEN",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Antiforgery request token paired with the session cookie.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
            if (operation.Responses?.TryGetValue("200", out var replayResponse) == true &&
                replayResponse is OpenApiResponse concreteReplayResponse)
            {
                concreteReplayResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
                concreteReplayResponse.Headers["Idempotent-Replay"] = new OpenApiHeader
                {
                    Description = "True when the response replays the committed assignment cancellation.",
                    Required = false,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                };
            }
        }
        if (operation.OperationId == "AssignReplacementLeader")
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-XSRF-TOKEN",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Antiforgery request token paired with the session cookie.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
            if (operation.Responses?.TryGetValue("200", out var replayResponse) == true &&
                replayResponse is OpenApiResponse concreteReplayResponse)
            {
                concreteReplayResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
                concreteReplayResponse.Headers["Idempotent-Replay"] = new OpenApiHeader
                {
                    Description = "True when the response replays the committed active replacement assignment.",
                    Required = false,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.Boolean },
                };
            }
        }
        if (operation.OperationId == "RescheduleEvent")
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "If-Match",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Base64-encoded rowversion of the event aggregate for optimistic concurrency.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = "X-XSRF-TOKEN",
                In = ParameterLocation.Header,
                Required = true,
                Description = "Antiforgery request token paired with the session cookie.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String },
            });
        }
        return Task.CompletedTask;
    });
    options.AddSchemaTransformer((schema, context, _) =>
    {
        if (context.JsonTypeInfo.Type.Name is "DraftWriteInput" or "DraftUpdateRequest" or "DraftEventResponse")
        {
            schema.Required?.Remove("notes");
        }
        if (context.JsonTypeInfo.Type.Name == "CairoTimeInput")
        {
            schema.Required?.Remove("offset");
        }
        return Task.CompletedTask;
    });
});
builder.Services.AddCarpediemAntiforgery();
builder.Services.AddCarpediemPersistence(builder.Configuration);
builder.Services.AddCarpediemAuthorization();
builder.Services.AddEventsModule();
builder.Services.AddStaffingModule();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers.TryGetValue("X-Correlation-ID", out var requested) &&
        Guid.TryParse(requested.ToString(), out var supplied)
        ? supplied
        : Guid.NewGuid();
    context.Items["CorrelationId"] = correlationId;
    context.Response.Headers["X-Correlation-ID"] = correlationId.ToString();
    await next();
});
app.UseSerilogRequestLogging(options =>
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapHealthChecks("/healthz");
app.MapSessionEndpoints();
app.MapDraftEventsEndpoints();
app.MapChiefLeaderAppointmentEndpoints();
app.MapChiefLeaderResponseEndpoints();
app.MapEventAvailabilityEndpoints();
app.MapEventTeamSelectionEndpoints();
app.MapEventDirectAssignmentEndpoints();
app.MapEventSelfCancellationEndpoints();
app.MapEventReplacementAssignmentEndpoints();
app.MapEventRescheduleEndpoints();

await app.Services.SeedDevelopmentIdentityAsync(app.Environment.IsDevelopment(), app.Configuration);
await app.RunAsync();

public partial class Program;
