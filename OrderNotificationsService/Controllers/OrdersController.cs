using Microsoft.AspNetCore.Mvc;
using OrderNotificationsService.Features.Common;
using OrderNotificationsService.Features.Orders.CreateOrder;
using OrderNotificationsService.Features.Orders.UpdateOrderStatus;

namespace OrderNotificationsService.Controllers
{
    [ApiController]
    public class OrdersController : ControllerBase
    {
        //<summary>
        // Endpoint to create a new order for a user. Accepts a CreateOrderRequest containing the UserId.
        //</summary>
        [HttpPost]
        [Route("api/create/order")]
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


        //<summary>
        // Endpoint to update the status of an existing order. Accepts the orderId as a route parameter and an UpdateOrderStatusRequest containing the new status.
        //</summary>
        [HttpPut]
        [Route("api/update/{orderId}/status")]
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

            var result = await handler.Handle(command);

            if (!result.IsSuccess)
            {
                return result.ErrorCode switch
                {
                    HandlerErrorCode.NotFound => NotFound(new { Message = "Order was not found." }),
                    _ => StatusCode(StatusCodes.Status500InternalServerError, new { Message = "An unexpected error occurred." })
                };
            }

            return Ok(new
            {
                OrderId = orderId,
                Status = request.Status
            });
        }
    }
}