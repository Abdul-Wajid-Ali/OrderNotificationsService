namespace OrderNotificationsService.Features.Notifications.GetUserNotifications
{
    public class GetUserNotificationsQuery
    {
        public Guid UserId { get; set; }

        public int PageSize { get; set; } = 20;
    }
}
