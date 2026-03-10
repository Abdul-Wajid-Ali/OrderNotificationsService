namespace OrderNotificationsService.Infrastructure.Correlation
{
    // Abstraction for accessing the current request correlation ID
    public interface ICorrelationContextAccessor
    {
        string CorrelationId { get; }
    }

    public class CorrelationContextAccessor : ICorrelationContextAccessor
    {
        // Async-local storage that flows with the current execution context
        private static readonly AsyncLocal<string?> CorrelationIdHolder = new();

        // Exposes the correlation ID for the current request scope
        public string CorrelationId => CorrelationIdHolder.Value ?? string.Empty;

        // Assigns correlation ID for the current async execution context
        public static void Set(string correlationId)
        {
            CorrelationIdHolder.Value = correlationId;
        }
    }
}