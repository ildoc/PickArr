using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pickarr.Core.Domain.Entities;

namespace Pickarr.Infrastructure.Persistence.Configurations;

public sealed class RecoEventLogConfiguration : IEntityTypeConfiguration<RecoEventLog>
{
    public void Configure(EntityTypeBuilder<RecoEventLog> builder)
    {
        builder.ToTable("RecoEventLog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Event).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Scope).HasMaxLength(32);
        builder.HasIndex(x => x.At);
        builder.HasIndex(x => new { x.TitleId, x.At });
        builder.HasOne(x => x.Title)
            .WithMany()
            .HasForeignKey(x => x.TitleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
