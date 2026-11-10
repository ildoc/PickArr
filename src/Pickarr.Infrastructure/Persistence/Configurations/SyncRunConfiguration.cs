using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pickarr.Core.Domain.Entities;

namespace Pickarr.Infrastructure.Persistence.Configurations;

public sealed class SyncRunConfiguration : IEntityTypeConfiguration<SyncRun>
{
    public void Configure(EntityTypeBuilder<SyncRun> builder)
    {
        builder.ToTable("SyncRuns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TriggeredBy).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.StartedAt).IsRequired();
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);
        builder.HasIndex(x => new { x.Kind, x.CatalogSourceId, x.Status });
        builder.HasIndex(x => x.StartedAt);
        builder.HasOne(x => x.CatalogSource)
            .WithMany()
            .HasForeignKey(x => x.CatalogSourceId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
