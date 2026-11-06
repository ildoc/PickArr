using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickArr.Core.Domain.Entities;

namespace PickArr.Infrastructure.Persistence.Configurations;

public sealed class RatingConfiguration : IEntityTypeConfiguration<Rating>
{
    public void Configure(EntityTypeBuilder<Rating> builder)
    {
        builder.ToTable("Ratings");
        builder.HasKey(x => x.TitleId);
        builder.Property(x => x.Value).HasMaxLength(16).IsRequired();
        builder.HasOne(x => x.Title)
            .WithOne(x => x.Rating)
            .HasForeignKey<Rating>(x => x.TitleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
