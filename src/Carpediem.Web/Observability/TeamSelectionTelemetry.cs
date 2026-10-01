using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Carpediem.Web.Observability;

public static class TeamSelectionTelemetry
{
    public const string SourceName = "Carpediem.TeamSelection";

    private static readonly ActivitySource Activities = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>(
        "carpediem.team_selection.outcomes");

    public static Activity? Start(string operation = "Event.SelectVolunteer") =>
        Activities.StartActivity(operation);

    public static void Record(Activity? activity, string outcome)
    {
        activity?.SetTag("team_selection.outcome", outcome);
        Outcomes.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
    }
}
