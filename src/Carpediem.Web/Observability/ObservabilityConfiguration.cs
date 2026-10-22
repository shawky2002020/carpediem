using Carpediem.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
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

        builder.Services.AddHealthChecks()
            .AddCheck<DatabaseReadinessHealthCheck>("sqlserver", tags: ["ready"]);
        return builder;
    }

    private static void AddTelemetry(WebApplicationBuilder builder)
    {
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing
                .AddSource(ChiefLeaderAppointmentTelemetry.SourceName)
                .AddSource(EventPublicationTelemetry.SourceName)
                .AddSource(EventAvailabilityTelemetry.SourceName)
                .AddSource(TeamSelectionTelemetry.SourceName)
                .AddSource(DirectAssignmentTelemetry.SourceName)
                .AddSource(LeaderSelfCancellationTelemetry.SourceName)
                .AddSource(ReplacementAssignmentTelemetry.ActivitySourceName)
                .AddSource(EventRescheduleTelemetry.ActivitySourceName)
                .AddSource(EventCompletionTelemetry.ActivitySourceName)
                .AddSource(AttendanceTelemetry.ActivitySourceName)
                .AddAspNetCoreInstrumentation(options =>
            {
                options.RecordException = true;
                options.Filter = httpContext =>
                    !httpContext.Request.Path.StartsWithSegments("/healthz") &&
                    !httpContext.Request.Path.StartsWithSegments("/api/health");
            }))
            .WithMetrics(metrics => metrics
                .AddMeter(ChiefLeaderAppointmentTelemetry.SourceName)
                .AddMeter(EventPublicationTelemetry.SourceName)
                .AddMeter(EventAvailabilityTelemetry.SourceName)
                .AddMeter(TeamSelectionTelemetry.SourceName)
                .AddMeter(DirectAssignmentTelemetry.SourceName)
                .AddMeter(LeaderSelfCancellationTelemetry.SourceName)
                .AddMeter(ReplacementAssignmentTelemetry.MeterName)
                .AddMeter(EventRescheduleTelemetry.MeterName)
                .AddMeter(EventCompletionTelemetry.MeterName)
                .AddMeter(AttendanceTelemetry.MeterName)
                .AddAspNetCoreInstrumentation());

    }

    private sealed class DatabaseReadinessHealthCheck(CarpediemDbContext dbContext) : IHealthCheck
    {
        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("Database connection verified.")
                : HealthCheckResult.Unhealthy("Database connection unavailable.");
        }
    }
}
