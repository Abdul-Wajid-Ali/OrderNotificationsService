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

            // Load any existing notifications created from this event to ensure idempotency.
            var existingNotifications = await _dbContext.Notifications
                .Where(x => x.SourceEventId == sourceEventId)
                .ToListAsync(cancellationToken);

            // Create an in-app notification if one does not already exist.
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

            // Create an email notification record if one does not already exist.
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

            // If the email was already sent in a previous retry attempt, skip sending again.
            if (emailNotification.DeliveryStatus == NotificationDeliveryStatus.Sent)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // Increment attempt count before trying to deliver the email.
            emailNotification.DeliveryAttemptCount += 1;

            // Clear any previous delivery error before retrying.
            emailNotification.LastDeliveryError = null;

            try
            {
                // Attempt to send the email notification through the configured email service.
                await _emailSender.SendOrderStatusChangeAsync(evt.UserId, evt.OrderId, message, cancellationToken);

                // Mark the notification as successfully delivered.
                emailNotification.DeliveryStatus = NotificationDeliveryStatus.Sent;
                emailNotification.DeliveredAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Persist failure details so the retry mechanism can inspect and retry later.
                emailNotification.DeliveryStatus = NotificationDeliveryStatus.Failed;
                emailNotification.LastDeliveryError = ex.Message;

                await _dbContext.SaveChangesAsync(cancellationToken);

                // Rethrow so the Outbox processor can trigger its retry policy.
                throw;
            }
        }
    }
}