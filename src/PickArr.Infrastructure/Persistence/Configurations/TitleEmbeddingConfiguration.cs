using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pickarr.Core.Domain.Entities;

namespace Pickarr.Infrastructure.Persistence.Configurations;

public sealed class TitleEmbeddingConfiguration : IEntityTypeConfiguration<TitleEmbedding>
{
    public void Configure(EntityTypeBuilder<TitleEmbedding> builder)
    {
        builder.ToTable("TitleEmbeddings");
        builder.HasKey(x => new { x.TitleId, x.ModelId });
        builder.Property(x => x.ModelId).HasMaxLength(128).IsRequired();
        builder.Property(x => x.TextHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Vector).IsRequired();
        builder.HasIndex(x => x.ModelId);
        builder.HasOne(x => x.Title)
            .WithMany()
            .HasForeignKey(x => x.TitleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
