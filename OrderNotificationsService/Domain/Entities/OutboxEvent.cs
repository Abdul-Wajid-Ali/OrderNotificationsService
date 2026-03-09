namespace OrderNotificationsService.Domain.Entities
{
    public class OutboxEvent
    {
        public Guid Id { get; set; }

        public string EventType { get; set; }

        public string Payload { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ProcessedAt { get; set; }

        public int RetryCount { get; set; }

        public DateTime? NextRetryAt { get; set; }

        public DateTime? DeadLetteredAt { get; set; }

        public string? LastError { get; set; }
    }
}