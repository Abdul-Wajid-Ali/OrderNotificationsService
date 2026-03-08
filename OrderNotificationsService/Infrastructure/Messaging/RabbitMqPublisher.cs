using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace OrderNotificationsService.Infrastructure.Messaging
{
    public class RabbitMqPublisher
    {
        private readonly RabbitMqOptions _rabbitMqOptions;
        private IConnection? _connection;
        private IChannel? _channel;
        private bool _initialized;
        private readonly SemaphoreSlim _initLock = new(1, 1);

        public RabbitMqPublisher(IOptions<RabbitMqOptions> rabbitMqOptions)
        {
            _rabbitMqOptions = rabbitMqOptions.Value;
        }

        private async Task InitializeAsync()
        {
            // Fast path to skip lock acquisition when connection is already ready.
            if (_initialized) return;

            await _initLock.WaitAsync();

            try
            {
                // Double-check initialization state after entering the lock.
                if (_initialized) return;

                // Create connection factory for local RabbitMQ broker.
                var factory = new ConnectionFactory
                {
                    HostName = _rabbitMqOptions.HostName,
                };

                // Open a single shared connection and channel for publishing.
                _connection = await factory.CreateConnectionAsync();
                _channel = await _connection.CreateChannelAsync();

                // Ensure the fanout exchange exists before any publish attempt.
                await _channel.ExchangeDeclareAsync(
                    exchange: _rabbitMqOptions.ExchangeName,
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
            // Lazily initialize RabbitMQ objects on first publish call.
            await InitializeAsync();

            // Serialize the event payload to UTF-8 JSON bytes.
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            // Mark message as durable JSON so consumers can parse reliably.
            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent
            };

            // Publish to fanout exchange so all bound queues receive the event.
            await _channel!.BasicPublishAsync(
                exchange: _rabbitMqOptions.ExchangeName,
                routingKey: "",
                mandatory: false,
                basicProperties: props,
                body: body);
        }
    }
}