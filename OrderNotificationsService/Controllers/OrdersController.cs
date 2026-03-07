using Microsoft.AspNetCore.Mvc;
using OrderNotificationsService.Features.Orders.CreateOrder;
using OrderNotificationsService.Features.Orders.UpdateOrderStatus;

namespace OrderNotificationsService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> CreateOrder(CreateOrderRequest request,
        [FromServices] CreateOrderHandler handler)
        {
            var command = new CreateOrderCommand
            {
                UserId = request.UserId
            };

            var orderId = await handler.Handle(command);

            return Ok(new { OrderId = orderId });
        }

        [HttpPut("{orderId}/status")]
        public async Task<IActionResult> UpdateOrderStatus(
            Guid orderId,
            UpdateOrderStatusRequest request,
            [FromServices] UpdateOrderStatusHandler handler)
        {
            var command = new UpdateOrderStatusCommand
            {
                OrderId = orderId,
                NewStatus = request.Status
            };

            await handler.Handle(command);

            return Ok(new
            {
                OrderId = orderId,
                Status = request.Status
            });
        }
    }
}