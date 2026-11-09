using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickArr.Core.Domain.Entities;

namespace PickArr.Infrastructure.Persistence.Configurations;

public sealed class WatchedConfiguration : IEntityTypeConfiguration<Watched>
{
    public void Configure(EntityTypeBuilder<Watched> builder)
    {
        builder.ToTable("Watched");
        builder.HasKey(x => x.TitleId);
        builder.HasOne(x => x.Title)
            .WithOne(x => x.Watched)
            .HasForeignKey<Watched>(x => x.TitleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
