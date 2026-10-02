namespace Carpediem.Web.Workers;

public sealed class EventLifecycleWorkerOptions
{
    public const string SectionName = "EventLifecycleWorker";

    /// <summary>
    /// Gets or sets whether the automated completion background worker is enabled.
    /// Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the periodic scan interval in seconds.
    /// Default is 30 seconds.
    /// </summary>
    public int ScanIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum number of expired events to process per scan iteration.
    /// Default is 50.
    /// </summary>
    public int BatchSize { get; set; } = 50;

    public TimeSpan ScanInterval => TimeSpan.FromSeconds(Math.Max(1, ScanIntervalSeconds));
}
