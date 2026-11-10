using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pickarr.Core.Domain.Entities;

namespace Pickarr.Infrastructure.Persistence.Configurations;

public sealed class RatingEventConfiguration : IEntityTypeConfiguration<RatingEvent>
{
    public void Configure(EntityTypeBuilder<RatingEvent> builder)
    {
        builder.ToTable("RatingEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Value).HasMaxLength(16);
        builder.Property(x => x.Origin).HasMaxLength(32).IsRequired();
        builder.HasIndex(x => new { x.TitleId, x.At });
        builder.HasOne(x => x.Title)
            .WithMany()
            .HasForeignKey(x => x.TitleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Profile)
            .WithMany()
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
