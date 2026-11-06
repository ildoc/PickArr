using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickArr.Core.Domain.Entities;

namespace PickArr.Infrastructure.Persistence.Configurations;

public sealed class RecoStateConfiguration : IEntityTypeConfiguration<RecoState>
{
    public void Configure(EntityTypeBuilder<RecoState> builder)
    {
        builder.ToTable("RecoState");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Stage).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ModelStatus).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ModelId).HasMaxLength(128);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.LastEvalJson);
        builder.Property(x => x.Level3Active).HasDefaultValue(false);
    }
}
