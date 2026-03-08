using Microsoft.EntityFrameworkCore;
using OrderNotificationsService.Domain.Events;
using OrderNotificationsService.Features.Notifications.ProcessOrderStatusChanged;
using OrderNotificationsService.Infrastructure.Persistence;
using System.Text.Json;

namespace OrderNotificationsService.Infrastructure.BackgroundServices
{
    public class OutboxProcessor : BackgroundService
    {
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

            // Pull only unprocessed events in creation order so processing is deterministic.
            var events = await db.OutboxEvents
                .Where(x => x.ProcessedAt == null)
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
                            continue;
                        }

                        // Delegate business logic to the notification handler.
                        await orderStatusChangedHandler.Handle(domainEvent, token);
                    }
                    else
                    {
                        _logger.LogWarning("No handler configured for event type {EventType}", evt.EventType);
                    }

                    // Mark event as processed only after successful handling.
                    evt.ProcessedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    // Log and continue so the event can be retried on the next pass.
                    _logger.LogError(ex, "Failed processing event {EventId}", evt.Id);
                }
            }

            // Persist processing markers for all events in this batch.
            await db.SaveChangesAsync(token);
        }
    }
}