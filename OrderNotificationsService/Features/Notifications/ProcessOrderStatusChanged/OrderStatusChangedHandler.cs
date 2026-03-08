using OrderNotificationsService.Domain.Events;

namespace OrderNotificationsService.Features.Notifications.ProcessOrderStatusChanged
{
    public class OrderStatusChangedHandler
    {
        public async Task Handle(OrderStatusChangedEvent evt)
        {
            // send email
            Console.WriteLine($"Email sent to user {evt.UserId}");

            // create in-app notification
            Console.WriteLine($"In-app notification created");
        }
    }
}