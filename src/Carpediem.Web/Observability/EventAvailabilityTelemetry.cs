using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Carpediem.Web.Observability;

public static class EventAvailabilityTelemetry
{
    public const string SourceName = "Carpediem.EventAvailability";

    private static readonly ActivitySource Activities = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>(
        "carpediem.event_availability.outcomes");

    public static Activity? Start() => Activities.StartActivity(
        "Event.DeclareAvailability");

    public static void Record(Activity? activity, string outcome)
    {
        activity?.SetTag("event_availability.outcome", outcome);
        Outcomes.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
    }
}
