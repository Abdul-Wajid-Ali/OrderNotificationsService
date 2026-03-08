using Microsoft.EntityFrameworkCore;
using OrderNotificationsService.Infrastructure.Messaging;
using OrderNotificationsService.Infrastructure.Persistence;

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

                await Task.Delay(5000, stoppingToken);
            }
        }

        private async Task ProcessOutboxEvents(CancellationToken token)
        {
            using var scope = _serviceProvider.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var events = await db.OutboxEvents
                .Where(x => x.ProcessedAt == null)
                .Take(20)
                .ToListAsync(token);

            foreach (var evt in events)
            {
                try
                {
                    _logger.LogInformation("Processing event {EventId}", evt.Id);

                    var publisher = scope.ServiceProvider.GetRequiredService<RabbitMqPublisher>();

                    await publisher.PublishAsync(evt);

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