using OrderNotificationsService.Domain.Enums;

namespace OrderNotificationsService.Domain.Entities
{
    public class Notification
    {
        public Guid Id { get; set; }

        public Guid SourceEventId { get; set; }

        public Guid UserId { get; set; }

        public Guid OrderId { get; set; }

        public string Message { get; set; }

        public NotificationType Type { get; set; }

        public NotificationDeliveryStatus DeliveryStatus { get; set; }

        public int DeliveryAttemptCount { get; set; }

        public string? LastDeliveryError { get; set; }

        public DateTime? DeliveredAt { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}