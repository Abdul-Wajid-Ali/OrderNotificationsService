using Microsoft.EntityFrameworkCore;
using OrderNotificationsService.Domain.Entities;

namespace OrderNotificationsService.Infrastructure.Persistence
{
    // EF Core database context representing the application's persistence boundary
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // Table representing orders created within the system
        public DbSet<Order> Orders => Set<Order>();

        // Table storing user notification records
        public DbSet<Notification> Notifications => Set<Notification>();

        // Table implementing the transactional outbox for reliable event publishing
        public DbSet<OutboxEvent> OutboxEvents => Set<OutboxEvent>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Automatically apply entity configurations defined in the same assembly
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }
    }
}