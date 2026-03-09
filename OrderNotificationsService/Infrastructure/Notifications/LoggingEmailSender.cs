namespace OrderNotificationsService.Infrastructure.Notifications
{
    public class LoggingEmailSender : IEmailSender
    {
        private readonly ILogger<LoggingEmailSender> _logger;

        public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
        {
            _logger = logger;
        }

        public Task SendOrderStatusChangeAsync(Guid userId, Guid orderId, string message, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Sent order status email to user {UserId} for order {OrderId}. Message: {Message}", userId, orderId, message);
            return Task.CompletedTask;
        }
    }
}