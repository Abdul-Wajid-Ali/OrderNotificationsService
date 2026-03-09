using Microsoft.EntityFrameworkCore;
using OrderNotificationsService.Domain.Entities;
using OrderNotificationsService.Infrastructure.Persistence;

namespace OrderNotificationsService.Features.Notifications.GetUserNotifications
{
    // Implements GetUserNotificationsHandler behavior for the order notifications service.
    public class GetUserNotificationsHandler
    {
        private readonly AppDbContext _dbContext;

        public GetUserNotificationsHandler(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<Notification>> Handle(GetUserNotificationsQuery query, CancellationToken cancellationToken)
        {
            var pageSize = Math.Clamp(query.PageSize, 1, 100);

            return await _dbContext.Notifications
                .Where(x => x.UserId == query.UserId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(pageSize)
                .ToListAsync(cancellationToken);
        }
    }
}