using OrderNotificationsService.Domain.Events;

namespace OrderNotificationsService.Infrastructure.Contracts
{
    // Message envelope that wraps the domain event with metadata for messaging and tracing
    public class OrderStatusChangedEnvelope
    {
        // Identifier of the originating outbox event for traceability and idempotent processing
        public Guid OutboxEventId { get; set; }

        // Correlation identifier used to link logs and operations across services
        public string CorrelationId { get; set; } = string.Empty;

        // Distributed tracing identifier propagated from the originating request
        public string TraceId { get; set; } = string.Empty;

        // Domain event payload describing the order status change
        public OrderStatusChangedEvent Event { get; set; } = default!;
    }
}