using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Carpediem.Web.Observability;

public static class LeaderSelfCancellationTelemetry
{
    public const string SourceName = "Carpediem.LeaderSelfCancellation";

    private static readonly ActivitySource Activities = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>(
        "carpediem.leader_self_cancellation.outcomes");

    public static Activity? Start(string operation = "Event.CancelAssignment") =>
        Activities.StartActivity(operation);

    public static void Record(Activity? activity, string outcome)
    {
        activity?.SetTag("leader_self_cancellation.outcome", outcome);
        Outcomes.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
    }
}
