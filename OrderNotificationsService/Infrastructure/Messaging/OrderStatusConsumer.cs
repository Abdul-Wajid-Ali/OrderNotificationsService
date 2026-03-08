using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

namespace OrderNotificationsService.Infrastructure.Messaging
{
    public class OrderStatusConsumer : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory
            {
                HostName = "localhost"
            };

            var connection = await factory.CreateConnectionAsync(stoppingToken);
            var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.ExchangeDeclareAsync(
                exchange: "order-events",
                type: ExchangeType.Fanout,
                cancellationToken: stoppingToken);

            var queueResult = await channel.QueueDeclareAsync(cancellationToken: stoppingToken);
            var queueName = queueResult.QueueName;

            await channel.QueueBindAsync(
                queue: queueName,
                exchange: "order-events",
                routingKey: "",
                cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                Console.WriteLine($"Received event: {message}");

                // send email
                // create notification

                await Task.CompletedTask;
            };

            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: true,
                consumer: consumer,
                cancellationToken: stoppingToken);

            // keep service alive
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}