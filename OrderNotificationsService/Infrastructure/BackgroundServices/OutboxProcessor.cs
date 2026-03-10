using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderNotificationsService.Domain.Events;
using OrderNotificationsService.Infrastructure.Contracts;
using OrderNotificationsService.Infrastructure.Messaging;
using OrderNotificationsService.Infrastructure.Monitoring;
using OrderNotificationsService.Infrastructure.Persistence;
using System.Text.Json;

namespace OrderNotificationsService.Infrastructure.BackgroundServices
{
    public class OutboxProcessor : BackgroundService
    {
        private const int MaxRetries = 5;
        private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(5);
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxProcessor> _logger;
        private readonly NotificationPipelineMetrics _metrics;
        private readonly MonitoringOptions _monitoringOptions;
        private long _processedCount;
        private long _failedCount;

        public OutboxProcessor(
                IServiceProvider serviceProvider,
                ILogger<OutboxProcessor> logger,
                NotificationPipelineMetrics metrics,
                IOptions<MonitoringOptions> monitoringOptions)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _metrics = metrics;
            _monitoringOptions = monitoringOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Continuously poll the outbox until the service shuts down
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessOutboxEvents(stoppingToken);

                // Small delay to prevent constant database polling
                await Task.Delay(2000, stoppingToken);
            }
        }

        private async Task ProcessOutboxEvents(CancellationToken token)
        {
            // Create scoped services for this processing cycle
            using var scope = _serviceProvider.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var publisher = scope.ServiceProvider.GetRequiredService<RabbitMqPublisher>();

            var now = DateTime.UtcNow;

            // Fetch a batch of eligible outbox events ready for processing
            var events = await db.OutboxEvents
                .Where(x => x.ProcessedAt == null && x.DeadLetteredAt == null)
                .Where(x => x.NextRetryAt == null || x.NextRetryAt <= now)
                .OrderBy(x => x.CreatedAt)
                .Take(20)
                .ToListAsync(token);

            // Process events independently to isolate failures
            foreach (var evt in events)
            {
                var started = DateTime.UtcNow;

                try
                {
                    if (evt.EventType == nameof(OrderStatusChangedEvent))
                    {
                        // Rehydrate domain event from stored payload
                        var domainEvent = JsonSerializer.Deserialize<OrderStatusChangedEvent>(evt.Payload);

                        if (domainEvent == null)
                        {
                            _logger.LogWarning("Invalid event payload for outbox event {EventId}", evt.Id);
                            evt.ProcessedAt = DateTime.UtcNow;
                            continue;
                        }

                        // Wrap event with tracing metadata for downstream systems
                        var envelope = new OrderStatusChangedEnvelope
                        {
                            OutboxEventId = evt.Id,
                            CorrelationId = domainEvent.CorrelationId,
                            TraceId = domainEvent.TraceId,
                            Event = domainEvent
                        };

                        // Publish event to the message broker
                        await publisher.PublishAsync(envelope, token);
                    }
                    else
                    {
                        _logger.LogWarning("No handler configured for event type {EventType}", evt.EventType);
                    }

                    // Mark event as successfully processed
                    evt.ProcessedAt = DateTime.UtcNow;
                    evt.LastError = null;
                    evt.NextRetryAt = null;

                    Interlocked.Increment(ref _processedCount);
                    _metrics.RecordProcessingLatency(DateTime.UtcNow - started);
                }
                catch (Exception ex)
                {
                    // Record failure and increment retry counter
                    evt.RetryCount += 1;
                    evt.LastError = ex.Message;

                    Interlocked.Increment(ref _failedCount);

                    if (evt.RetryCount >= MaxRetries)
                    {
                        // Move permanently failing events to dead-letter state
                        evt.DeadLetteredAt = DateTime.UtcNow;
                        evt.NextRetryAt = null;

                        _logger.LogError(
                            ex,
                            "Outbox event {EventId} moved to dead letter after {RetryCount} attempts",
                            evt.Id,
                            evt.RetryCount);
                    }
                    else
                    {
                        // Schedule retry using exponential backoff
                        var retryDelaySeconds = BaseRetryDelay.TotalSeconds * Math.Pow(2, evt.RetryCount - 1);

                        evt.NextRetryAt = DateTime.UtcNow.AddSeconds(Math.Min(retryDelaySeconds, 300));

                        _logger.LogError(
                            ex,
                            "Failed processing event {EventId}. Retrying at {NextRetryAt} (attempt {RetryCount}/{MaxRetries})",
                            evt.Id,
                            evt.NextRetryAt,
                            evt.RetryCount,
                            MaxRetries);
                    }
                }
            }

            // Persist processing results for the batch
            await db.SaveChangesAsync(token);

            // Update queue health metrics and evaluate alert conditions
            await UpdateBacklogMetricsAndAlerts(db, token);
            EvaluateFailureRateAlert();
        }

        private async Task UpdateBacklogMetricsAndAlerts(AppDbContext db, CancellationToken token)
        {
            // Query current backlog state of the outbox queue
            var pending = db.OutboxEvents
                .Where(x => x.ProcessedAt == null && x.DeadLetteredAt == null);

            var count = await pending.LongCountAsync(token);
            var oldest = await pending.MinAsync(x => (DateTime?)x.CreatedAt, token);
            var oldestAgeSeconds = oldest.HasValue ? (DateTime.UtcNow - oldest.Value).TotalSeconds : 0;

            // Record backlog size and age metrics
            _metrics.UpdateOutboxBacklog(count, oldestAgeSeconds);

            // Trigger alert when queue lag exceeds configured threshold
            if (oldestAgeSeconds >= _monitoringOptions.QueueLagAlertThresholdSeconds)
            {
                _logger.LogError(
                    "ALERT: outbox queue lag threshold exceeded. AgeSeconds={LagSeconds}, ThresholdSeconds={Threshold}",
                    oldestAgeSeconds,
                    _monitoringOptions.QueueLagAlertThresholdSeconds);
            }
        }

        private void EvaluateFailureRateAlert()
        {
            // Calculate failure rate based on processed vs failed events
            var processed = Interlocked.Read(ref _processedCount);
            var failed = Interlocked.Read(ref _failedCount);
            var total = processed + failed;

            if (total < _monitoringOptions.MinimumEventsForFailureRateAlert)
            {
                return;
            }

            var failureRate = total == 0 ? 0 : (failed / (double)total) * 100;

            // Emit alert if failure rate crosses monitoring threshold
            if (failureRate >= _monitoringOptions.FailureRateAlertThresholdPercent)
            {
                _logger.LogError(
                    "ALERT: outbox failure rate threshold exceeded. FailureRate={FailureRate:F2}%, Threshold={Threshold}%, Failed={Failed}, Total={Total}",
                    failureRate,
                    _monitoringOptions.FailureRateAlertThresholdPercent,
                    failed,
                    total);
            }
        }
    }
}