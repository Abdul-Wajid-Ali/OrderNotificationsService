namespace OrderNotificationsService.Infrastructure.Messaging
{
    // Configuration settings used to connect and publish events to RabbitMQ
    public class RabbitMqOptions
    {
        // Configuration section name used for binding settings from app configuration
        public const string SectionName = "RabbitMq";

        // Host address of the RabbitMQ broker
        public string HostName { get; set; } = "localhost";

        // Exchange used to publish order-related events
        public string ExchangeName { get; set; } = "order-events";
    }
}