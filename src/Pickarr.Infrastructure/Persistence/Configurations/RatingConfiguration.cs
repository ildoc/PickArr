using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pickarr.Core.Domain.Entities;

namespace Pickarr.Infrastructure.Persistence.Configurations;

public sealed class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> builder)
    {
        builder.ToTable("Ratings");
        builder.HasKey(x => new { x.ProfileId, x.TitleId });
        builder.Property(x => x.Value).HasMaxLength(16).IsRequired();
        builder.HasOne(x => x.Title)
            .WithMany(x => x.Ratings)
            .HasForeignKey(x => x.TitleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Profile)
            .WithMany()
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
