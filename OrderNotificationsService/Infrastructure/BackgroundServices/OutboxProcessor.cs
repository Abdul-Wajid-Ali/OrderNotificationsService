using Microsoft.EntityFrameworkCore;
using OrderNotificationsService.Domain.Events;
using OrderNotificationsService.Features.Notifications.ProcessOrderStatusChanged;
using OrderNotificationsService.Infrastructure.Persistence;
using System.Text.Json;

namespace OrderNotificationsService.Infrastructure.BackgroundServices
{
    public class OutboxProcessor : BackgroundService
    {
        private const int MaxRetries = 5;
        private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(5);
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OutboxProcessor> _logger;

        public OutboxProcessor(IServiceProvider serviceProvider, ILogger<OutboxProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Poll the outbox table continuously until the host shuts down.
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessOutboxEvents(stoppingToken);

                // Add a short delay so we do not hammer the database between batches.
                await Task.Delay(2000, stoppingToken);
            }
        }

        private async Task ProcessOutboxEvents(CancellationToken token)
        {
            // Resolve scoped dependencies for this processing iteration.
            using var scope = _serviceProvider.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var orderStatusChangedHandler = scope.ServiceProvider.GetRequiredService<OrderStatusChangedHandler>();

            var now = DateTime.UtcNow;

            // Pull only unprocessed events in creation order so processing is deterministic.
            var events = await db.OutboxEvents
                .Where(x => x.ProcessedAt == null && x.DeadLetteredAt == null)
                .Where(x => x.NextRetryAt == null || x.NextRetryAt <= now)
                .OrderBy(x => x.CreatedAt)
                .Take(20)
                .ToListAsync(token);

            // Process each event independently so one failure does not block the entire batch.
            foreach (var evt in events)
            {
                try
                {
                    if (evt.EventType == nameof(OrderStatusChangedEvent))
                    {
                        // Deserialize payload back to the expected domain event contract.
                        var domainEvent = JsonSerializer.Deserialize<OrderStatusChangedEvent>(evt.Payload);

                        if (domainEvent == null)
                        {
                            _logger.LogWarning("Invalid event payload for outbox event {EventId}", evt.Id);
                            evt.ProcessedAt = DateTime.UtcNow;
                            continue;
                        }

                        // Delegate business logic to the notification handler.
                        await orderStatusChangedHandler.Handle(domainEvent, evt.Id, token);
                    }
                    else
                    {
                        _logger.LogWarning("No handler configured for event type {EventType}", evt.EventType);
                    }

                    // Mark event as processed only after successful handling.
                    evt.ProcessedAt = DateTime.UtcNow; 
                    evt.LastError = null;
                    evt.NextRetryAt = null;
                }
                catch (Exception ex)
                {
                    evt.RetryCount += 1;
                    evt.LastError = ex.Message;

                    if (evt.RetryCount >= MaxRetries)
                    {
                        evt.DeadLetteredAt = DateTime.UtcNow;
                        evt.NextRetryAt = null;
                        _logger.LogError(ex, "Outbox event {EventId} moved to dead letter after {RetryCount} attempts", evt.Id, evt.RetryCount);
                    }
                    else
                    {
                        var retryDelaySeconds = BaseRetryDelay.TotalSeconds * Math.Pow(2, evt.RetryCount - 1);
                        evt.NextRetryAt = DateTime.UtcNow.AddSeconds(Math.Min(retryDelaySeconds, 300));
                        _logger.LogError(ex, "Failed processing event {EventId}. Retrying at {NextRetryAt} (attempt {RetryCount}/{MaxRetries})", evt.Id, evt.NextRetryAt, evt.RetryCount, MaxRetries);
                    }
                }
            }

            // Persist processing markers for all events in this batch.
            await db.SaveChangesAsync(token);
        }
    }
}