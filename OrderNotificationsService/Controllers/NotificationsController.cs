using Microsoft.AspNetCore.Mvc;
using OrderNotificationsService.Features.Notifications.GetUserNotifications;

namespace OrderNotificationsService.Controllers
{
    [ApiController]
    [Route("api/user/notifications")]
    public class NotificationsController : ControllerBase
    {
        //<summary>
        /// Retrieves a paginated list of notifications for a specific user.
        //</summary>
        [HttpGet("{userId:guid}")]
        public async Task<IActionResult> GetUserNotifications(
                    Guid userId,
                    [FromQuery] int pageSize,
                    [FromServices] GetUserNotificationsHandler handler,
                    CancellationToken cancellationToken)
        {
            var query = new GetUserNotificationsQuery
            {
                UserId = userId,
                PageSize = pageSize <= 0 ? 20 : pageSize
            };

            var notifications = await handler.Handle(query, cancellationToken);

            return Ok(notifications);
        }
    }
}