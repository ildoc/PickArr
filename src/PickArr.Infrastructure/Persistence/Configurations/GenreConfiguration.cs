using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pickarr.Core.Domain.Entities;

namespace Pickarr.Infrastructure.Persistence.Configurations;

public sealed class GenreConfiguration : IEntityTypeConfiguration<Genre>
{
    public void Configure(EntityTypeBuilder<Genre> builder)
    {
        builder.ToTable("Genres");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
    }
}

public sealed class GenreLocalizationConfiguration : IEntityTypeConfiguration<GenreLocalization>
{
    public void Configure(EntityTypeBuilder<GenreLocalization> builder)
    {
        builder.ToTable("GenreLocalizations");
        builder.HasKey(x => new { x.GenreId, x.Language });
        builder.Property(x => x.Language).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
        builder.HasOne(x => x.Genre)
            .WithMany(x => x.Localizations)
            .HasForeignKey(x => x.GenreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class TitleGenreConfiguration : IEntityTypeConfiguration<TitleGenre>
{
    public void Configure(EntityTypeBuilder<TitleGenre> builder)
    {
        builder.ToTable("TitleGenres");
        builder.HasKey(x => new { x.TitleId, x.GenreId });
        builder.HasOne(x => x.Title)
            .WithMany(x => x.TitleGenres)
            .HasForeignKey(x => x.TitleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Genre)
            .WithMany(x => x.TitleGenres)
            .HasForeignKey(x => x.GenreId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
