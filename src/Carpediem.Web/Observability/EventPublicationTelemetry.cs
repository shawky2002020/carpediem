using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Carpediem.Web.Observability;

public static class EventPublicationTelemetry
{
    public const string SourceName = "Carpediem.EventPublication";

    private static readonly ActivitySource Activities = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>(
        "carpediem.event_publication.outcomes");

    public static Activity? Start() => Activities.StartActivity(
        "Event.Publish");

    public static void Record(Activity? activity, string outcome)
    {
        activity?.SetTag("event_publication.outcome", outcome);
        Outcomes.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
    }
}
