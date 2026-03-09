using OrderNotificationsService.Domain.Entities;
using OrderNotificationsService.Domain.Events;
using OrderNotificationsService.Features.Common;
using OrderNotificationsService.Infrastructure.Persistence;
using System.Text.Json;

namespace OrderNotificationsService.Features.Orders.UpdateOrderStatus
{
    public class UpdateOrderStatusHandler
    {
        private readonly AppDbContext _dbContext;

        public UpdateOrderStatusHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<HandlerResult> Handle(UpdateOrderStatusCommand command)
        {
            // Retrieve the order being updated.
            var order = await _dbContext.Orders.FindAsync(command.OrderId);

            // Return a failure result if the order does not exist.
            if (order == null)
                return HandlerResult.Failure(HandlerErrorCode.NotFound);

            var oldStatus = order.Status;

            // Avoid unnecessary updates if the status is already the requested value.
            if (oldStatus == command.NewStatus)
                return HandlerResult.Success();

            // Update the order state and record the modification timestamp.
            order.Status = command.NewStatus;
            order.UpdatedAt = DateTime.UtcNow;

            // Create a domain event describing the state transition.
            var domainEvent = new OrderStatusChangedEvent
            {
                OrderId = order.Id,
                UserId = order.UserId,
                OldStatus = oldStatus,
                NewStatus = command.NewStatus,
                OccurredAt = DateTime.UtcNow
            };

            // Persist the event in the Outbox table to ensure reliable asynchronous processing.
            var outboxEvent = new OutboxEvent
            {
                Id = Guid.NewGuid(),
                EventType = nameof(OrderStatusChangedEvent),
                Payload = JsonSerializer.Serialize(domainEvent),
                CreatedAt = DateTime.UtcNow
            };

            // Add the outbox event to the current transaction.
            _dbContext.OutboxEvents.Add(outboxEvent);

            // Save both the order update and outbox event atomically.
            await _dbContext.SaveChangesAsync();

            return HandlerResult.Success();
        }
    }
}