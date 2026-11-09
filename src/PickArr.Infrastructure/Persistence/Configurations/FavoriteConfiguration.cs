using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pickarr.Core.Domain.Entities;

namespace Pickarr.Infrastructure.Persistence.Configurations;

public sealed class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.ToTable("Favorites");
        builder.HasKey(x => x.TitleId);
        builder.HasOne(x => x.Title)
            .WithOne(x => x.Favorite)
            .HasForeignKey<Favorite>(x => x.TitleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
