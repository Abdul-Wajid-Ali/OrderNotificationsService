using OrderNotificationsService.Domain.Enums;

namespace OrderNotificationsService.Features.Orders.UpdateOrderStatus
{
    public class UpdateOrderStatusCommand
    {
        public Guid OrderId { get; set; }

        public OrderStatus NewStatus { get; set; }
    }
}