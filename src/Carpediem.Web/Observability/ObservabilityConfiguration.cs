using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Serilog;
using System.Globalization;

namespace Carpediem.Web.Observability;

public static class ObservabilityConfiguration
{
    public static WebApplicationBuilder AddCarpediemObservability(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, services, logger) => logger
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture));

        if (!builder.Environment.IsEnvironment("Testing"))
        {
            AddTelemetry(builder);
        }

        builder.Services.AddHealthChecks();
        return builder;
    }

    private static void AddTelemetry(WebApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = httpContext => !httpContext.Request.Path.StartsWithSegments("/healthz");
            }))
            .WithMetrics(metrics => metrics.AddAspNetCoreInstrumentation());

    }
}
