namespace OrderNotificationsService.Infrastructure.Messaging
{
    public class RabbitMqOptions
    {
        public const string SectionName = "RabbitMq";

        public string HostName { get; set; } = "localhost";
        public string ExchangeName { get; set; } = "order-events";
    }
}
