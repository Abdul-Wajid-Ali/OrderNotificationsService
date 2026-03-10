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
            // Initialize a new order entity with default status and timestamps
            var order = new Order
            {
                Id = Guid.NewGuid(),
                UserId = command.UserId,
                Status = OrderStatus.Placed,
                CreatedAt = DateTime.UtcNow
            };

            // Stage the new order for persistence
            _dbContext.Orders.Add(order);

            // Persist the order to the database
            await _dbContext.SaveChangesAsync();

            // Return the generated order identifier
            return order.Id;
        }
    }
}