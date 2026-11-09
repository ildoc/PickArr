using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pickarr.Core.Domain.Entities;

namespace Pickarr.Infrastructure.Persistence.Configurations;

public sealed class TitleLocalizationConfiguration : IEntityTypeConfiguration<TitleLocalization>
{
    public void Configure(EntityTypeBuilder<TitleLocalization> builder)
    {
        builder.ToTable("TitleLocalizations");
        builder.HasKey(x => new { x.TitleId, x.Language });
        builder.Property(x => x.Language).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(512).IsRequired();
        builder.Property(x => x.NameNormalized).HasMaxLength(512).IsRequired();
        builder.HasIndex(x => x.NameNormalized);
        builder.HasOne(x => x.Title)
            .WithMany(x => x.Localizations)
            .HasForeignKey(x => x.TitleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
