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
            // Build user-facing notification message from the event data
            var message = $"Your order {evt.OrderId} changed from {evt.OldStatus} to {evt.NewStatus}.";

            // Load previously created notifications for this event to guarantee idempotent processing
            var existingNotifications = await _dbContext.Notifications
                .Where(x => x.SourceEventId == sourceEventId)
                .ToListAsync(cancellationToken);

            // Ensure an in-app notification exists for the user
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

            // Ensure an email notification record exists to track delivery attempts
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

            // Skip delivery if the email notification was already successfully sent
            if (emailNotification.DeliveryStatus == NotificationDeliveryStatus.Sent)
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
                return;
            }

            // Prepare notification for another delivery attempt
            emailNotification.DeliveryAttemptCount += 1;
            emailNotification.LastDeliveryError = null;

            try
            {
                // Send email notification through the configured email delivery service
                await _emailSender.SendOrderStatusChangeAsync(evt.UserId, evt.OrderId, message, cancellationToken);

                // Mark email notification as successfully delivered
                emailNotification.DeliveryStatus = NotificationDeliveryStatus.Sent;
                emailNotification.DeliveredAt = DateTime.UtcNow;

                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                // Record delivery failure so retry mechanisms can reattempt later
                emailNotification.DeliveryStatus = NotificationDeliveryStatus.Failed;
                emailNotification.LastDeliveryError = ex.Message;

                await _dbContext.SaveChangesAsync(cancellationToken);

                // Propagate failure so the upstream outbox processor can trigger its retry policy
                throw;
            }
        }
    }
}