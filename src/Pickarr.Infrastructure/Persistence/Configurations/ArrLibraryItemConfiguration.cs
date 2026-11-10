using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pickarr.Core.Domain.Entities;

namespace Pickarr.Infrastructure.Persistence.Configurations;

public sealed class ArrLibraryItemConfiguration : IEntityTypeConfiguration<ArrLibraryItem>
{
    public void Configure(EntityTypeBuilder<ArrLibraryItem> builder)
    {
        builder.ToTable("ArrLibraryItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Kind).HasMaxLength(16).IsRequired();
        builder.Property(x => x.ArrTitle).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ImdbId).HasMaxLength(32);
        builder.Property(x => x.State).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Path).HasMaxLength(1024);
        builder.HasIndex(x => new { x.Kind, x.ArrId }).IsUnique();
        builder.HasIndex(x => x.TmdbId);
        builder.HasIndex(x => x.TvdbId);
        builder.HasIndex(x => x.TitleId);
        builder.HasOne(x => x.Title)
            .WithMany()
            .HasForeignKey(x => x.TitleId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class ArrSeasonStateConfiguration : IEntityTypeConfiguration<ArrSeasonState>
{
    public void Configure(EntityTypeBuilder<ArrSeasonState> builder)
    {
        builder.ToTable("ArrSeasonStates");
        builder.HasKey(x => new { x.ArrLibraryItemId, x.SeasonNumber });
        builder.Property(x => x.State).HasMaxLength(32).IsRequired();
        builder.HasOne(x => x.ArrLibraryItem)
            .WithMany(x => x.Seasons)
            .HasForeignKey(x => x.ArrLibraryItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
