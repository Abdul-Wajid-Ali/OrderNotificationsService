namespace OrderNotificationsService.Infrastructure.Monitoring
{
    public class MonitoringOptions
    {
        // Configuration section name used for binding monitoring settings
        public const string SectionName = "Monitoring";

        // Failure percentage that triggers an alert when processing events
        public double FailureRateAlertThresholdPercent { get; set; } = 25;

        // Maximum allowed age of the oldest pending outbox event before alerting
        public double QueueLagAlertThresholdSeconds { get; set; } = 120;

        // Minimum number of processed events required before evaluating failure rate alerts
        public int MinimumEventsForFailureRateAlert { get; set; } = 20;
    }
}