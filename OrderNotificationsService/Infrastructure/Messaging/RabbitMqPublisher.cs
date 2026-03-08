using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace OrderNotificationsService.Infrastructure.Messaging
{
    public class RabbitMqPublisher
    {
        private IConnection? _connection;
        private IChannel? _channel;
        private bool _initialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        private async Task InitializeAsync()
        {
            if (_initialized) return;

            await _initLock.WaitAsync();

            try
            {
                if (_initialized) return;

                var factory = new ConnectionFactory
                {
                    HostName = "localhost"
                };

                _connection = await factory.CreateConnectionAsync();
                _channel = await _connection.CreateChannelAsync();

                await _channel.ExchangeDeclareAsync(
                    exchange: "order-events",
                    type: ExchangeType.Fanout);

                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task PublishAsync<T>(T message)
        {
            await InitializeAsync();

            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent
            };

            await _channel!.BasicPublishAsync(
                exchange: "order-events",
                routingKey: "",
                mandatory: false,
                basicProperties: props,
                body: body);
        }
    }
}