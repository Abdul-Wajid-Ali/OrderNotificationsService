using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OrderNotificationsService.Domain.Entities;
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
                await ProcessSingleOutboxEvent(evt, db, publisher, token);
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

            // Calculate pending outbox count and age of the oldest event to measure queue lag
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

            Console.WriteLine("wajid ali here :: ", failureRate);

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

        /// Processes a single outbox event and handles success, retry, or dead-letter outcomes.
        private async Task ProcessSingleOutboxEvent(
            OutboxEvent evt,
            AppDbContext db,
            RabbitMqPublisher publisher,
            CancellationToken token)
        {
            var started = DateTime.UtcNow;

            try
            {
                // Determine which domain event type is stored in the outbox
                if (evt.EventType == nameof(OrderStatusChangedEvent))
                {
                    // Rehydrate domain event from the stored JSON payload
                    var domainEvent = JsonSerializer.Deserialize<OrderStatusChangedEvent>(evt.Payload);

                    // Guard against corrupted or incompatible payloads
                    if (domainEvent == null)
                    {
                        _logger.LogWarning("Invalid event payload for outbox event {EventId}", evt.Id);
                        evt.ProcessedAt = DateTime.UtcNow;
                        return;
                    }

                    // Wrap the domain event in an envelope that carries
                    // correlation and tracing metadata for downstream consumers
                    var envelope = new OrderStatusChangedEnvelope
                    {
                        OutboxEventId = evt.Id,
                        CorrelationId = domainEvent.CorrelationId,
                        TraceId = domainEvent.TraceId,
                        Event = domainEvent
                    };

                    // Publish the event to the message broker (RabbitMQ)
                    await publisher.PublishAsync(envelope, token);
                }
                else
                {
                    // Defensive logging in case an unknown event type appears in the outbox
                    _logger.LogWarning("No handler configured for event type {EventType}", evt.EventType);
                }

                // Mark the event as successfully processed
                evt.ProcessedAt = DateTime.UtcNow;
                evt.LastError = null;
                evt.NextRetryAt = null;

                // Update success metrics and processing latency
                Interlocked.Increment(ref _processedCount);
                _metrics.RecordProcessingLatency(DateTime.UtcNow - started);
            }
            catch (Exception ex)
            {
                // Record failure and increment retry attempt counter
                evt.RetryCount += 1;
                evt.LastError = ex.Message;

                Interlocked.Increment(ref _failedCount);

                if (evt.RetryCount >= MaxRetries)
                {
                    // Permanently failing events are moved to a dead-letter state
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
                    // Schedule the next retry using exponential backoff
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
    }
}