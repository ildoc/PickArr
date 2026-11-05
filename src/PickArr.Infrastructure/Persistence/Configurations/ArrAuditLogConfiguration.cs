using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickArr.Core.Domain.Entities;

namespace PickArr.Infrastructure.Persistence.Configurations;

public sealed class ArrAuditLogConfiguration : IEntityTypeConfiguration<ArrAuditLog>
{
    public void Configure(EntityTypeBuilder<ArrAuditLog> builder)
    {
        builder.ToTable("ArrAuditLogs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Kind).HasMaxLength(16).IsRequired();
        builder.Property(x => x.Options).IsRequired();
        builder.Property(x => x.Outcome).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Error).HasMaxLength(2048);
        builder.HasIndex(x => x.At);
        builder.HasIndex(x => x.TitleId);
    }
}
