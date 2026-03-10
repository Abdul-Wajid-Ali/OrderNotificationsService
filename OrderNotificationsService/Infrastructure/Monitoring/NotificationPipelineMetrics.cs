using System.Diagnostics.Metrics;

namespace OrderNotificationsService.Infrastructure.Monitoring
{
    public class NotificationPipelineMetrics
    {
        // Counters tracking message publishing outcomes
        private readonly Counter<long> _publishSuccess;
        private readonly Counter<long> _publishFailure;

        // Counters tracking consumer processing outcomes
        private readonly Counter<long> _consumerSuccess;
        private readonly Counter<long> _consumerFailure;

        // Histogram measuring end-to-end notification processing latency
        private readonly Histogram<double> _processingLatencyMs;

        // Backlog metrics representing the current state of the outbox queue
        private long _outboxBacklogCount;
        private double _outboxOldestAgeSeconds;

        public NotificationPipelineMetrics(IMeterFactory meterFactory)
        {
            // Create meter used to emit telemetry for the notification pipeline
            var meter = meterFactory.Create("OrderNotificationsService.NotificationPipeline");

            // Counters recording publishing success and failure events
            _publishSuccess = meter.CreateCounter<long>("outbox_publish_success_total");
            _publishFailure = meter.CreateCounter<long>("outbox_publish_failure_total");

            // Counters recording consumer processing outcomes
            _consumerSuccess = meter.CreateCounter<long>("consumer_process_success_total");
            _consumerFailure = meter.CreateCounter<long>("consumer_process_failure_total");

            // Histogram capturing how long notification processing takes
            _processingLatencyMs = meter.CreateHistogram<double>("notification_processing_latency_ms");

            // Observable gauges exposing real-time outbox backlog metrics
            meter.CreateObservableGauge<long>("outbox_backlog_count", () => _outboxBacklogCount);
            meter.CreateObservableGauge<double>("outbox_backlog_oldest_age_seconds", () => _outboxOldestAgeSeconds);
        }

        // Record successful publish operation
        public void IncrementPublishSuccess() => _publishSuccess.Add(1);

        // Record failed publish operation
        public void IncrementPublishFailure() => _publishFailure.Add(1);

        // Record successful consumer processing
        public void IncrementConsumerSuccess() => _consumerSuccess.Add(1);

        // Record failed consumer processing
        public void IncrementConsumerFailure() => _consumerFailure.Add(1);

        // Track latency of notification processing
        public void RecordProcessingLatency(TimeSpan elapsed) => _processingLatencyMs.Record(elapsed.TotalMilliseconds);

        // Update snapshot values used by backlog gauges
        public void UpdateOutboxBacklog(long count, double oldestAgeSeconds)
        {
            Interlocked.Exchange(ref _outboxBacklogCount, count);
            Interlocked.Exchange(ref _outboxOldestAgeSeconds, oldestAgeSeconds);
        }
    }
}