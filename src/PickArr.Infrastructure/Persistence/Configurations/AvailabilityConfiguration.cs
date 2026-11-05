using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickArr.Core.Domain.Entities;

namespace PickArr.Infrastructure.Persistence.Configurations;

public sealed class AvailabilityConfiguration : IEntityTypeConfiguration<Availability>
{
    public void Configure(EntityTypeBuilder<Availability> builder)
    {
        builder.ToTable("Availabilities");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.TitleId, x.CatalogSourceId }).IsUnique();
        builder.HasIndex(x => new { x.CatalogSourceId, x.RemovedAt, x.AddedAt });
        builder.HasOne(x => x.Title)
            .WithMany(x => x.Availabilities)
            .HasForeignKey(x => x.TitleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CatalogSource)
            .WithMany()
            .HasForeignKey(x => x.CatalogSourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
