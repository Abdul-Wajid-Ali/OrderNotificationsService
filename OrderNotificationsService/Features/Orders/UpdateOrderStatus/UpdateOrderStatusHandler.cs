using OrderNotificationsService.Domain.Entities;
using OrderNotificationsService.Domain.Events;
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

        public async Task Handle(UpdateOrderStatusCommand command)
        {
            var order = await _dbContext.Orders.FindAsync(command.OrderId);

            if (order == null)
                throw new Exception("Order not found");

            var oldStatus = order.Status;

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
        }
    }
}
