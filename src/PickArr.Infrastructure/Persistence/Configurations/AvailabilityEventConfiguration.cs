using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickArr.Core.Domain.Entities;

namespace PickArr.Infrastructure.Persistence.Configurations;

public sealed class AvailabilityEventConfiguration : IEntityTypeConfiguration<AvailabilityEvent>
{
    public void Configure(EntityTypeBuilder<AvailabilityEvent> builder)
    {
        builder.ToTable("AvailabilityEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Details).HasMaxLength(2000);
        builder.HasIndex(x => x.OccurredAt);
        builder.HasOne(x => x.Availability)
            .WithMany(x => x.Events)
            .HasForeignKey(x => x.AvailabilityId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
