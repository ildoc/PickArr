using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickArr.Core.Domain.Entities;

namespace PickArr.Infrastructure.Persistence.Configurations;

public sealed class CatalogSourceConfiguration : IEntityTypeConfiguration<CatalogSource>
{
    public void Configure(EntityTypeBuilder<CatalogSource> builder)
    {
        builder.ToTable("CatalogSources");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CountryCode).HasMaxLength(2).IsRequired();
        builder.Property(x => x.MetadataLanguage).HasMaxLength(16).IsRequired();
        builder.Property(x => x.IncrementalCron).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FullSyncCron).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.ProviderId, x.CountryCode }).IsUnique();
        builder.HasOne(x => x.Provider)
            .WithMany(x => x.CatalogSources)
            .HasForeignKey(x => x.ProviderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
