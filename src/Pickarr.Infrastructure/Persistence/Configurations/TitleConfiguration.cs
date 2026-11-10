using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pickarr.Core.Domain.Entities;

namespace Pickarr.Infrastructure.Persistence.Configurations;

public sealed class TitleConfiguration : IEntityTypeConfiguration<Title>
{
    public void Configure(EntityTypeBuilder<Title> builder)
    {
        builder.ToTable("Titles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WatchmodeId);
        builder.HasIndex(x => x.WatchmodeId).IsUnique();
        builder.Property(x => x.TmdbType).HasMaxLength(16);
        builder.Property(x => x.ImdbId).HasMaxLength(32);
        builder.HasIndex(x => x.ImdbId);
        builder.Property(x => x.MediaType).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(16).IsRequired();
        builder.Property(x => x.OriginalTitle).HasMaxLength(512);
        builder.Property(x => x.OriginalTitleNormalized).HasMaxLength(512);
        builder.HasIndex(x => x.OriginalTitleNormalized);
        builder.Property(x => x.OriginalLanguage).HasMaxLength(16);
        builder.Property(x => x.PosterPath).HasMaxLength(256);
        builder.Property(x => x.BackdropPath).HasMaxLength(256);
        builder.Property(x => x.MetadataStatus).HasMaxLength(16).IsRequired();
        builder.HasIndex(x => x.MetadataStatus);
        builder.HasIndex(x => x.ReleaseYear);
        builder.HasIndex(x => x.TvdbId);
        builder.HasIndex(x => new { x.TmdbType, x.TmdbId })
            .IsUnique()
            .HasFilter("\"TmdbId\" IS NOT NULL");
    }
}
