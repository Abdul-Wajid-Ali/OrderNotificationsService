using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace OrderNotificationsService.Infrastructure.Messaging
{
    public class OrderStatusConsumer : BackgroundService
    {
        private readonly RabbitMqOptions _rabbitMqOptions;

        public OrderStatusConsumer(IOptions<RabbitMqOptions> rabbitMqOptions)
        {
            _rabbitMqOptions = rabbitMqOptions.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Build RabbitMQ connection settings for the local broker.
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMqOptions.HostName,
            };

            // Open connection and channel used by this background consumer.
            var connection = await factory.CreateConnectionAsync(stoppingToken);
            var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // Declare the exchange so the consumer can bind even after broker restarts.
            await channel.ExchangeDeclareAsync(
                exchange: _rabbitMqOptions.ExchangeName,
                type: ExchangeType.Fanout,
                cancellationToken: stoppingToken);

            // Create a temporary queue for this consumer instance.
            var queueResult = await channel.QueueDeclareAsync(cancellationToken: stoppingToken);
            var queueName = queueResult.QueueName;

            // Bind queue to fanout exchange to receive all published order events.
            await channel.QueueBindAsync(
                queue: queueName,
                exchange: _rabbitMqOptions.ExchangeName,
                routingKey: "",
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                // Decode incoming message body for downstream processing.
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Console.WriteLine($"Received event: {message}");

                // Process the received event.
                // Email notification logic and in-app notification creation
                // will be implemented here based on the event data.

                await Task.CompletedTask;
            };

            // Start consuming messages until cancellation is requested.
            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: true,
                consumer: consumer,
                cancellationToken: stoppingToken);

            // Keep this hosted service alive for the lifetime of the application.
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}