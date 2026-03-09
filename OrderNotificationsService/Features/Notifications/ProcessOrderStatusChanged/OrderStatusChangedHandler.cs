using Microsoft.EntityFrameworkCore;
using OrderNotificationsService.Domain.Entities;
using OrderNotificationsService.Domain.Enums;
using OrderNotificationsService.Domain.Events;
using OrderNotificationsService.Infrastructure.Notifications;
using OrderNotificationsService.Infrastructure.Persistence;

namespace OrderNotificationsService.Features.Notifications.ProcessOrderStatusChanged
{
    public class OrderStatusChangedHandler
    {
        private readonly AppDbContext _dbContext;
        private readonly IEmailSender _emailSender;

        public OrderStatusChangedHandler(AppDbContext dbContext, IEmailSender emailSender)
        {
            _dbContext = dbContext;
            _emailSender = emailSender;
        }

        public async Task Handle(OrderStatusChangedEvent evt, Guid sourceEventId, CancellationToken cancellationToken)
        {
            var message = $"Your order {evt.OrderId} changed from {evt.OldStatus} to {evt.NewStatus}.";

            var existingNotifications = await _dbContext.Notifications
                .Where(x => x.SourceEventId == sourceEventId)
                .ToListAsync(cancellationToken);

            var inAppNotification = existingNotifications.FirstOrDefault(x => x.Type == NotificationType.InApp);
            if (inAppNotification == null)
            {
                inAppNotification = new Notification
                {
                    Id = Guid.NewGuid(),
                    SourceEventId = sourceEventId,
                    UserId = evt.UserId,
                    OrderId = evt.OrderId,
                    Message = message,
                    Type = NotificationType.InApp,
                    DeliveryStatus = NotificationDeliveryStatus.Sent,
                    DeliveryAttemptCount = 0,
                    DeliveredAt = DateTime.UtcNow,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Notifications.Add(inAppNotification);
            }

            var emailNotification = existingNotifications.FirstOrDefault(x => x.Type == NotificationType.Email);
            if (emailNotification == null)
            {
                emailNotification = new Notification
                {
                    Id = Guid.NewGuid(),
                    SourceEventId = sourceEventId,
                    UserId = evt.UserId,
                    OrderId = evt.OrderId,
                    Message = message,
                    Type = NotificationType.Email,
                    DeliveryStatus = NotificationDeliveryStatus.Pending,
                    DeliveryAttemptCount = 0,
                    IsRead = true,
                    CreatedAt = DateTime.UtcNow
                };

                _dbContext.Notifications.Add(emailNotification);
            }

            if (emailNotification.DeliveryStatus == NotificationDeliveryStatus.Sent)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            emailNotification.DeliveryAttemptCount += 1;

            emailNotification.LastDeliveryError = null;

            try
            {
                await _emailSender.SendOrderStatusChangeAsync(evt.UserId, evt.OrderId, message, cancellationToken);
                emailNotification.DeliveryStatus = NotificationDeliveryStatus.Sent;
                emailNotification.DeliveredAt = DateTime.UtcNow;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                emailNotification.DeliveryStatus = NotificationDeliveryStatus.Failed;
                emailNotification.LastDeliveryError = ex.Message;
                await _dbContext.SaveChangesAsync(cancellationToken);
                throw;
            }
        }
    }
}