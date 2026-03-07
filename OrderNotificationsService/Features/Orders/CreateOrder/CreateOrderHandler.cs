using OrderNotificationsService.Domain.Entities;
using OrderNotificationsService.Domain.Enums;
using OrderNotificationsService.Infrastructure.Persistence;

namespace OrderNotificationsService.Features.Orders.CreateOrder
{
    public class CreateOrderHandler
    {
        private readonly AppDbContext _dbContext;

        public CreateOrderHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<Guid> Handle(CreateOrderCommand command)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = command.UserId,
                Status = OrderStatus.Placed,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Orders.Add(order);

            await _dbContext.SaveChangesAsync();

            return order.Id;
        }
    }
}