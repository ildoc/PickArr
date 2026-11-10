using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pickarr.Core.Domain.Entities;

namespace Pickarr.Infrastructure.Persistence.Configurations;

public sealed class WatchedConfiguration : IEntityTypeConfiguration<Watched>
{
    public void Configure(EntityTypeBuilder<Watched> builder)
    {
        builder.ToTable("Watched");
        builder.HasKey(x => new { x.ProfileId, x.TitleId });
        builder.HasOne(x => x.Title)
            .WithMany(x => x.WatchedItems)
            .HasForeignKey(x => x.TitleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Profile)
            .WithMany()
            .HasForeignKey(x => x.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
