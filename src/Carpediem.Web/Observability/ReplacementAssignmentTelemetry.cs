using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Carpediem.Web.Observability;

public static class ReplacementAssignmentTelemetry
{
    public const string ActivitySourceName = "Carpediem.Staffing.ReplacementAssignment";
    public const string MeterName = "Carpediem.Staffing.ReplacementAssignment";

    public static readonly ActivitySource Source = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> OperationsCounter = Meter.CreateCounter<long>(
        "carpediem_staffing_replacement_assignment_operations_total",
        unit: "{operation}",
        description: "Number of replacement leader assignment operations executed by outcome.");

    public static void Record(Activity? activity, string outcome)
    {
        activity?.SetTag("carpediem.replacement_assignment.outcome", outcome);
        OperationsCounter.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
    }
}
