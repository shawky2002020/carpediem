using Carpediem.Infrastructure.Persistence;
using Carpediem.Modules.Events;
using Carpediem.Modules.Identity;
using Carpediem.Modules.Identity.Infrastructure;
using Carpediem.Web.Endpoints;
using Carpediem.Web.Errors;
using Carpediem.Web.Observability;
using Carpediem.Web.Security;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.AddCarpediemObservability();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddCarpediemAntiforgery();
builder.Services.AddCarpediemPersistence(builder.Configuration);
builder.Services.AddCarpediemAuthorization();
builder.Services.AddEventsModule();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.Use(async (context, next) =>
{
    if (context.Request.Headers.TryGetValue("X-Correlation-ID", out var requested) &&
        !string.IsNullOrWhiteSpace(requested))
    {
        context.TraceIdentifier = requested.ToString();
    }

    context.Response.Headers["X-Correlation-ID"] = context.TraceIdentifier;
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

await app.Services.SeedDevelopmentIdentityAsync(app.Environment.IsDevelopment(), app.Configuration);
await app.RunAsync();

public partial class Program;
