using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderNotificationsService.Domain.Entities;

namespace OrderNotificationsService.Infrastructure.Persistence.Configurations
{
    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.ToTable("Notifications");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.SourceEventId)
                   .IsRequired();

            builder.Property(x => x.Message)
                   .HasMaxLength(500)
                   .IsRequired();

            builder.Property(x => x.Type)
                   .IsRequired();

            builder.Property(x => x.DeliveryStatus)
                  .IsRequired();

            builder.Property(x => x.DeliveryAttemptCount)
                   .IsRequired();

            builder.Property(x => x.LastDeliveryError)
                   .HasMaxLength(1000);

            builder.Property(x => x.CreatedAt)
                   .IsRequired();

            builder.Property(x => x.IsRead)
                   .IsRequired();

            builder.HasIndex(x => x.UserId);

            builder.HasIndex(x => new { x.SourceEventId, x.Type }).IsUnique();
        }
    }
}