using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Carpediem.Web.Observability;

public static class EventCompletionTelemetry
{
    public const string ActivitySourceName = "Carpediem.Events.Completion";
    public const string MeterName = "Carpediem.Events.Completion";

    public static readonly ActivitySource Source = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> OperationsCounter = Meter.CreateCounter<long>(
        "carpediem_events_completion_operations_total",
        unit: "{operation}",
        description: "Number of event completion operations executed by source and outcome.");

    public static void Record(Activity? activity, string source, string outcome)
    {
        activity?.SetTag("carpediem.events_completion.source", source);
        activity?.SetTag("carpediem.events_completion.outcome", outcome);
        OperationsCounter.Add(1,
            new KeyValuePair<string, object?>("source", source),
            new KeyValuePair<string, object?>("outcome", outcome));
    }
}
