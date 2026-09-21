using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events;
using Carpediem.Modules.Identity;
using Carpediem.Modules.Identity.Infrastructure;
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

await app.Services.SeedDevelopmentIdentityAsync(app.Environment.IsDevelopment(), app.Configuration);
await app.RunAsync();

public partial class Program;
