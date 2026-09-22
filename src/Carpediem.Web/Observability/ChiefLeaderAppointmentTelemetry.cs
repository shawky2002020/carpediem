using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Carpediem.Web.Observability;

public static class ChiefLeaderAppointmentTelemetry
{
    public const string SourceName = "Carpediem.ChiefLeaderAppointment";

    private static readonly ActivitySource Activities = new(SourceName);
    private static readonly Meter Meter = new(SourceName);
    private static readonly Counter<long> Outcomes = Meter.CreateCounter<long>(
        "carpediem.chief_leader_appointment.outcomes");

    public static Activity? Start() => Activities.StartActivity("ChiefLeaderAppointment.Appoint");

    public static void Record(Activity? activity, string outcome)
    {
        activity?.SetTag("appointment.outcome", outcome);
        Outcomes.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
    }
}
