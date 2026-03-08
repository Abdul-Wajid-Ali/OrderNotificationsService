using OrderNotificationsService.Domain.Entities;
using OrderNotificationsService.Domain.Enums;
using OrderNotificationsService.Domain.Events;
using OrderNotificationsService.Infrastructure.Persistence;

namespace OrderNotificationsService.Features.Notifications.ProcessOrderStatusChanged
{
    public class OrderStatusChangedHandler
    {
        private readonly AppDbContext _dbContext;
        private readonly ILogger<OrderStatusChangedHandler> _logger;

        public OrderStatusChangedHandler(AppDbContext dbContext, ILogger<OrderStatusChangedHandler> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task Handle(OrderStatusChangedEvent evt, CancellationToken cancellationToken)
        {
            var message = $"Your order {evt.OrderId} changed from {evt.OldStatus} to {evt.NewStatus}.";

            var inAppNotification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = evt.UserId,
                OrderId = evt.OrderId,
                Message = message,
                Type = NotificationType.InApp,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            var emailNotification = new Notification
            {
                Id = Guid.NewGuid(),
                UserId = evt.UserId,
                OrderId = evt.OrderId,
                Message = message,
                Type = NotificationType.Email,
                IsRead = true,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Notifications.Add(inAppNotification);
            _dbContext.Notifications.Add(emailNotification);

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Simulated email notification for user {UserId} and order {OrderId}", evt.UserId, evt.OrderId);
        }
    }
}