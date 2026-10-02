using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Carpediem.Web.Observability;

public static class AttendanceTelemetry
{
    public const string ActivitySourceName = "Carpediem.Attendance";
    public const string MeterName = "Carpediem.Attendance";

    public static readonly ActivitySource Source = new(ActivitySourceName);
    public static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> OperationsCounter = Meter.CreateCounter<long>(
        "carpediem_attendance_operations_total",
        unit: "{operation}",
        description: "Number of attendance operations executed by operation name and outcome.");

    public static void Record(Activity? activity, string operation, string outcome)
    {
        activity?.SetTag("carpediem.attendance.operation", operation);
        activity?.SetTag("carpediem.attendance.outcome", outcome);
        OperationsCounter.Add(1,
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("outcome", outcome));
    }
}
