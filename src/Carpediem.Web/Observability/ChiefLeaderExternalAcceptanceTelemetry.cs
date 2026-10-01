using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Carpediem.Web.Observability;

public static class ChiefLeaderExternalAcceptanceTelemetry
{
    public const string SourceName = "Carpediem.ChiefLeaderExternalAcceptance";

    private static readonly ActivitySource Activities = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>(
        "carpediem.chief_leader_external_acceptance.outcomes");

    public static Activity? Start() => Activities.StartActivity(
        "ChiefLeaderAssignment.RecordExternalAcceptance");

    public static void Record(Activity? activity, string outcome)
    {
        activity?.SetTag("external_acceptance.outcome", outcome);
        Outcomes.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
    }
}
