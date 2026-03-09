namespace OrderNotificationsService.Features.Notifications.GetUserNotifications
{
    // Implements GetUserNotificationsQuery behavior for the order notifications service.
    public class GetUserNotificationsQuery
    {
        public Guid UserId { get; set; }

        public int PageSize { get; set; } = 20;
    }
}
