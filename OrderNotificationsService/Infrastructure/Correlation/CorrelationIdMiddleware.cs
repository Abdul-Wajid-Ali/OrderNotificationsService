using System.Diagnostics;

namespace OrderNotificationsService.Infrastructure.Correlation
{
    public class CorrelationIdMiddleware
    {
        // Header used to propagate correlation IDs across services
        public const string CorrelationHeader = "X-Correlation-ID";

        private readonly RequestDelegate _next;

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Resolve correlation ID from incoming request or generate a new one
            var correlationId = context.Request.Headers.TryGetValue(CorrelationHeader, out var provided)
                ? provided.ToString()
                : Guid.NewGuid().ToString("N");

            // Attach correlation ID to the response so downstream clients can trace the request
            context.Response.Headers[CorrelationHeader] = correlationId;

            // Store correlation ID in the current execution context for later access
            CorrelationContextAccessor.Set(correlationId);

            // Enrich distributed tracing activity with correlation metadata
            Activity.Current?.SetTag("correlation.id", correlationId);
            Activity.Current?.SetTag("http.request_id", context.TraceIdentifier);

            // Continue executing the remaining middleware pipeline
            await _next(context);
        }
    }
}