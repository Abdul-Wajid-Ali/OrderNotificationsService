using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OrderNotificationsService.Domain.Entities;

namespace OrderNotificationsService.Infrastructure.Persistence.Configurations
{
    public class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
    {
        public void Configure(EntityTypeBuilder<OutboxEvent> builder)
        {
            builder.ToTable("OutboxEvents");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.EventType)
                   .HasMaxLength(200)
                   .IsRequired();

            builder.Property(x => x.Payload)
                   .IsRequired();

            builder.Property(x => x.CreatedAt)
                   .IsRequired();

            builder.Property(x => x.ProcessedAt);

            builder.Property(x => x.RetryCount)
                  .IsRequired();

            builder.Property(x => x.LastError)
                   .HasMaxLength(2000);

            builder.HasIndex(x => x.ProcessedAt);

            builder.HasIndex(x => x.DeadLetteredAt);

            builder.HasIndex(x => new { x.ProcessedAt, x.DeadLetteredAt, x.NextRetryAt });
        }
    }
}