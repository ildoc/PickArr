using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pickarr.Core.Domain.Entities;

namespace Pickarr.Infrastructure.Persistence.Configurations;

public sealed class TitleTmdbRecommendationConfiguration : IEntityTypeConfiguration<TitleTmdbRecommendation>
{
    public void Configure(EntityTypeBuilder<TitleTmdbRecommendation> builder)
    {
        builder.ToTable("TitleTmdbRecommendations");
        builder.HasKey(x => new { x.SourceTitleId, x.RelatedTitleId });
        builder.Property(x => x.Rank).IsRequired();
        builder.HasIndex(x => x.RelatedTitleId);
        builder.HasOne(x => x.SourceTitle)
            .WithMany(x => x.TmdbRecommendationsOut)
            .HasForeignKey(x => x.SourceTitleId)
            .OnDelete(DeleteBehavior.Cascade);
        // Restrict avoids SQLite/EF multiple-cascade-path issues; titles are rarely deleted.
        builder.HasOne(x => x.RelatedTitle)
            .WithMany(x => x.TmdbRecommendationsIn)
            .HasForeignKey(x => x.RelatedTitleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
