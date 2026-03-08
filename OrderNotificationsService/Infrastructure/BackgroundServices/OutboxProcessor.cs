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
            while (!stoppingToken.IsCancellationRequested)
            {
                await ProcessOutboxEvents(stoppingToken);

                await Task.Delay(2000, stoppingToken);
            }
        }

        private async Task ProcessOutboxEvents(CancellationToken token)
        {
            using var scope = _serviceProvider.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var orderStatusChangedHandler = scope.ServiceProvider.GetRequiredService<OrderStatusChangedHandler>();

            var events = await db.OutboxEvents
                .Where(x => x.ProcessedAt == null)
                .OrderBy(x => x.CreatedAt)
                .Take(20)
                .ToListAsync(token);

            foreach (var evt in events)
            {
                try
                {
                    if (evt.EventType == nameof(OrderStatusChangedEvent))
                    {
                        var domainEvent = JsonSerializer.Deserialize<OrderStatusChangedEvent>(evt.Payload);

                        if (domainEvent == null)
                        {
                            _logger.LogWarning("Invalid event payload for outbox event {EventId}", evt.Id);
                            continue;
                        }

                        await orderStatusChangedHandler.Handle(domainEvent, token);
                    }
                    else
                    {
                        _logger.LogWarning("No handler configured for event type {EventType}", evt.EventType);
                    }

                    evt.ProcessedAt = DateTime.UtcNow;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed processing event {EventId}", evt.Id);
                }
            }

            await db.SaveChangesAsync(token);
        }
    }
}