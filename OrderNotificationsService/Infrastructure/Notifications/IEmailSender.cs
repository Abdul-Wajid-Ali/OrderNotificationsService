namespace OrderNotificationsService.Infrastructure.Notifications
{
    public interface IEmailSender
    {
        Task SendOrderStatusChangeAsync(Guid userId, Guid orderId, string message, CancellationToken cancellationToken);
    }
}