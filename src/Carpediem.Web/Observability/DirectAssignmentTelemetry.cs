using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Carpediem.Web.Observability;

public static class DirectAssignmentTelemetry
{
    public const string SourceName = "Carpediem.DirectAssignment";

    private static readonly ActivitySource Activities = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>(
        "carpediem.direct_assignment.outcomes");

    public static Activity? Start(string operation = "Event.DirectAssignLeader") =>
        Activities.StartActivity(operation);

    public static void Record(Activity? activity, string outcome)
    {
        activity?.SetTag("direct_assignment.outcome", outcome);
        Outcomes.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
    }
}
