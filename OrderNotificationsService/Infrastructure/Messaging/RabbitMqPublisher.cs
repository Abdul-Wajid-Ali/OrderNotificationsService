using Microsoft.Extensions.Options;
using OrderNotificationsService.Infrastructure.Contracts;
using OrderNotificationsService.Infrastructure.Monitoring;
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
        private readonly NotificationPipelineMetrics _metrics;

        public RabbitMqPublisher(IOptions<RabbitMqOptions> rabbitMqOptions, NotificationPipelineMetrics metrics)
        {
            _metrics = metrics;
            _rabbitMqOptions = rabbitMqOptions.Value;
        }

        private async Task InitializeAsync()
        {
            // Skip initialization if connection and channel are already created
            if (_initialized) return;

            // Ensure only one thread performs RabbitMQ initialization
            await _initLock.WaitAsync();

            try
            {
                // Double-check initialization state after acquiring the lock
                if (_initialized) return;

                // Configure connection factory using application RabbitMQ settings
                var factory = new ConnectionFactory
                {
                    HostName = _rabbitMqOptions.HostName,
                };

                // Establish shared connection and channel used for publishing events
                _connection = await factory.CreateConnectionAsync();
                _channel = await _connection.CreateChannelAsync();

                // Ensure the configured exchange exists before publishing messages
                await _channel.ExchangeDeclareAsync(
                    exchange: _rabbitMqOptions.ExchangeName,
                    type: ExchangeType.Fanout,
                    durable: true);

                _initialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task PublishAsync(OrderStatusChangedEnvelope message, CancellationToken cancellationToken)
        {
            // Simulate transient failure for testing retryn and error handling logic in the notification pipeline
            //throw new Exception("Simulated failure for retry testing");
            //throw new Exception("Simulated publishing failure");

            // Lazily initialize RabbitMQ infrastructure on first publish
            await InitializeAsync();

            // Serialize event envelope into UTF-8 JSON payload
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            // Configure message metadata for durability and traceability
            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                CorrelationId = message.CorrelationId,
                MessageId = message.OutboxEventId.ToString()
            };

            // Attach distributed tracing information to message headers
            props.Headers = new Dictionary<string, object?>
            {
                ["trace_id"] = message.TraceId
            };

            try
            {
                // Publish event to fanout exchange so all bound queues receive it
                await _channel!.BasicPublishAsync(
                    exchange: _rabbitMqOptions.ExchangeName,
                    routingKey: string.Empty,
                    mandatory: false,
                    basicProperties: props,
                    body: body,
                    cancellationToken: cancellationToken);

                _metrics.IncrementPublishSuccess();
            }
            catch
            {
                // Record failure metrics and rethrow publishing error
                _metrics.IncrementPublishFailure();
                throw;
            }
        }
    }
}