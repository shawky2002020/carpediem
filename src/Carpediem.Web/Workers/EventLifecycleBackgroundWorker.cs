using Carpediem.Modules.Events.Application.Completion;
using Carpediem.Web.Observability;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Carpediem.Web.Workers;

public sealed partial class EventLifecycleBackgroundWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<EventLifecycleWorkerOptions> options,
    ILogger<EventLifecycleBackgroundWorker> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly EventLifecycleWorkerOptions config = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.Enabled)
        {
            LogWorkerDisabled(logger);
            return;
        }

        LogWorkerStarted(logger, config.ScanIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var activity = EventCompletionTelemetry.Source.StartActivity("ProcessScheduledCompletions");
                using var scope = scopeFactory.CreateScope();
                var completionStore = scope.ServiceProvider.GetRequiredService<IEventCompletionStore>();
                var utcNow = timeProvider.GetUtcNow();

                var completedCount = await completionStore.ProcessScheduledCompletionsAsync(utcNow, stoppingToken);

                if (completedCount > 0)
                {
                    LogBatchCompleted(logger, completedCount);
                    EventCompletionTelemetry.Record(activity, "SystemLifecycleWorker", "CompletedBatch");
                }
                else
                {
                    EventCompletionTelemetry.Record(activity, "SystemLifecycleWorker", "NoEligibleEvents");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                LogWorkerError(logger, ex);
            }

            try
            {
                await Task.Delay(config.ScanInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        LogWorkerStopped(logger);
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Information, Message = "EventLifecycleBackgroundWorker is disabled by configuration.")]
    private static partial void LogWorkerDisabled(ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "EventLifecycleBackgroundWorker started. Scan interval: {IntervalSeconds}s.")]
    private static partial void LogWorkerStarted(ILogger logger, int intervalSeconds);

    [LoggerMessage(EventId = 3, Level = LogLevel.Information, Message = "EventLifecycleBackgroundWorker transitioned {Count} published event(s) to Completed.")]
    private static partial void LogBatchCompleted(ILogger logger, int count);

    [LoggerMessage(EventId = 4, Level = LogLevel.Error, Message = "Unexpected error in EventLifecycleBackgroundWorker scan.")]
    private static partial void LogWorkerError(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information, Message = "EventLifecycleBackgroundWorker stopped.")]
    private static partial void LogWorkerStopped(ILogger logger);
}
