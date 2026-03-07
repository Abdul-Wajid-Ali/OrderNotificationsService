using OrderNotificationsService.Domain.Enums;

namespace OrderNotificationsService.Features.Orders.UpdateOrderStatus
{
    public class UpdateOrderStatusRequest
    {
        public OrderStatus Status { get; set; }
    }
}
