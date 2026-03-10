using OrderNotificationsService.Domain.Entities;
using OrderNotificationsService.Domain.Events;
using OrderNotificationsService.Features.Common;
using OrderNotificationsService.Infrastructure.Correlation;
using OrderNotificationsService.Infrastructure.Persistence;
using System.Diagnostics;
using System.Text.Json;

namespace OrderNotificationsService.Features.Orders.UpdateOrderStatus
{
    public class UpdateOrderStatusHandler
    {
        private readonly AppDbContext _dbContext;
        private readonly ICorrelationContextAccessor _correlationContextAccessor;

        public UpdateOrderStatusHandler(AppDbContext dbContext, ICorrelationContextAccessor correlationContextAccessor)
        {
            _dbContext = dbContext;
            _correlationContextAccessor = correlationContextAccessor;
        }

        public async Task<HandlerResult> Handle(UpdateOrderStatusCommand command)
        {
            // Load the target order from persistence
            var order = await _dbContext.Orders.FindAsync(command.OrderId);

            // Fail fast if the order does not exist
            if (order == null)
                return HandlerResult.Failure(HandlerErrorCode.NotFound);

            var oldStatus = order.Status;

            // Skip processing if the requested status is already applied
            if (oldStatus == command.NewStatus)
                return HandlerResult.Success();

            // Apply the status transition and update modification metadata
            order.Status = command.NewStatus;
            order.UpdatedAt = DateTime.UtcNow;

            // Create domain event describing the status change
            var domainEvent = new OrderStatusChangedEvent
            {
                OrderId = order.Id,
                UserId = order.UserId,
                OldStatus = oldStatus,
                NewStatus = command.NewStatus,
                OccurredAt = DateTime.UtcNow,
                CorrelationId = _correlationContextAccessor.CorrelationId,
                TraceId = Activity.Current?.TraceId.ToString() ?? string.Empty
            };

            // Wrap domain event into an outbox record for reliable event publishing
            var outboxEvent = new OutboxEvent
            {
                Id = Guid.NewGuid(),
                EventType = nameof(OrderStatusChangedEvent),
                Payload = JsonSerializer.Serialize(domainEvent),
                CreatedAt = DateTime.UtcNow
            };

            // Queue the event for asynchronous processing
            _dbContext.OutboxEvents.Add(outboxEvent);

            // Persist order update and outbox event in a single transaction
            await _dbContext.SaveChangesAsync();

            return HandlerResult.Success();
        }
    }
}