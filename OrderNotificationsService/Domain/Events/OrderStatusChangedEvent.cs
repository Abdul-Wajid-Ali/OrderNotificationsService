using OrderNotificationsService.Domain.Enums;

namespace OrderNotificationsService.Domain.Events
{
    public class OrderStatusChangedEvent
    {
        public Guid OrderId { get; set; }

        public Guid UserId { get; set; }

        public OrderStatus OldStatus { get; set; }

        public OrderStatus NewStatus { get; set; }

        public DateTime OccurredAt { get; set; }

        public string CorrelationId { get; set; } = string.Empty;

        public string TraceId { get; set; } = string.Empty;
    }
}