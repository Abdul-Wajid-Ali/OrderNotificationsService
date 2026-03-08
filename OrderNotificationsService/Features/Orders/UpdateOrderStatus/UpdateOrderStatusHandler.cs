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
            var order = await _dbContext.Orders.FindAsync(command.OrderId);

            if (order == null)
                return HandlerResult.Failure(HandlerErrorCode.NotFound);

            var oldStatus = order.Status;

            if (oldStatus == command.NewStatus)
                return HandlerResult.Success();

            order.Status = command.NewStatus;
            order.UpdatedAt = DateTime.UtcNow;

            var domainEvent = new OrderStatusChangedEvent
            {
                OrderId = order.Id,
                UserId = order.UserId,
                OldStatus = oldStatus,
                NewStatus = command.NewStatus,
                OccurredAt = DateTime.UtcNow
            };

            var outboxEvent = new OutboxEvent
            {
                Id = Guid.NewGuid(),
                EventType = nameof(OrderStatusChangedEvent),
                Payload = JsonSerializer.Serialize(domainEvent),
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.OutboxEvents.Add(outboxEvent);

            await _dbContext.SaveChangesAsync();

            return HandlerResult.Success();
        }
    }
}
