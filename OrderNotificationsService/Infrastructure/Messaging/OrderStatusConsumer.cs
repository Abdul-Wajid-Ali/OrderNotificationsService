using Microsoft.Extensions.Options;
using OrderNotificationsService.Features.Notifications.ProcessOrderStatusChanged;
using OrderNotificationsService.Infrastructure.Contracts;
using OrderNotificationsService.Infrastructure.Monitoring;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace OrderNotificationsService.Infrastructure.Messaging
{
    public class OrderStatusConsumer : BackgroundService
    {
        // Activity source used for distributed tracing of consumer operations
        private static readonly ActivitySource ActivitySource = new("OrderNotificationsService.Consumer");

        private readonly RabbitMqOptions _rabbitMqOptions;
        private readonly IServiceProvider _serviceProvider;
        private readonly NotificationPipelineMetrics _metrics;
        private readonly ILogger<OrderStatusConsumer> _logger;

        public OrderStatusConsumer(
             IOptions<RabbitMqOptions> rabbitMqOptions,
             IServiceProvider serviceProvider,
             NotificationPipelineMetrics metrics,
             ILogger<OrderStatusConsumer> logger)
        {
            _rabbitMqOptions = rabbitMqOptions.Value;
            _serviceProvider = serviceProvider;
            _metrics = metrics;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Configure RabbitMQ connection using application settings
            var factory = new ConnectionFactory
            {
                HostName = _rabbitMqOptions.HostName,
            };

            // Establish broker connection and open a channel for consuming messages
            var connection = await factory.CreateConnectionAsync(stoppingToken);
            var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // Ensure exchange exists so events can be routed after broker restarts
            await channel.ExchangeDeclareAsync(
                exchange: _rabbitMqOptions.ExchangeName,
                type: ExchangeType.Fanout,
                durable: true,
                cancellationToken: stoppingToken);

            // Declare durable queue used by this notification consumer
            var queueResult = await channel.QueueDeclareAsync(
                queue: "order-notifications",
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: stoppingToken);

            var queueName = queueResult.QueueName;

            // Bind queue to exchange so all order status events are delivered here
            await channel.QueueBindAsync(
                queue: queueName,
                exchange: _rabbitMqOptions.ExchangeName,
                routingKey: string.Empty,
                cancellationToken: stoppingToken);

            // Create asynchronous consumer that reacts to incoming messages
            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (sender, ea) =>
            {
                // Decode and deserialize the incoming event envelope
                var started = DateTime.UtcNow;
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);

                var envelope = JsonSerializer.Deserialize<OrderStatusChangedEnvelope>(message);
                if (envelope == null)
                {
                    _logger.LogWarning("Consumer received invalid payload: {Payload}", message);
                    _metrics.IncrementConsumerFailure();
                    return;
                }

                // Start tracing span for this message consumption
                using var activity = ActivitySource.StartActivity("consume.order-status-changed", ActivityKind.Consumer);
                activity?.SetTag("correlation.id", envelope.CorrelationId);
                activity?.SetTag("outbox.event_id", envelope.OutboxEventId);
                activity?.SetTag("trace.id.from_api", envelope.TraceId);

                // Attach correlation metadata to logs for observability
                using var logScope = _logger.BeginScope(new Dictionary<string, object>
                {
                    ["CorrelationId"] = envelope.CorrelationId,
                    ["OutboxEventId"] = envelope.OutboxEventId
                });

                try
                {
                    // Resolve handler and process the domain event
                    using var scope = _serviceProvider.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<OrderStatusChangedHandler>();

                    await handler.Handle(envelope.Event, envelope.OutboxEventId, stoppingToken);

                    _metrics.IncrementConsumerSuccess();
                    _metrics.RecordProcessingLatency(DateTime.UtcNow - started);
                }
                catch (Exception ex)
                {
                    // Record failure metrics and log processing error
                    _logger.LogError(ex, "Failed consuming event {OutboxEventId}", envelope.OutboxEventId);
                    _metrics.IncrementConsumerFailure();
                }
            };

            // Begin consuming messages from the queue
            await channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: true,
                consumer: consumer,
                cancellationToken: stoppingToken);

            // Keep the background worker alive for the lifetime of the host
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}