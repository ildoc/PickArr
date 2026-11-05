using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PickArr.Core.Domain.Entities;

namespace PickArr.Infrastructure.Persistence.Configurations;

public sealed class ApiUsageConfiguration : IEntityTypeConfiguration<ApiUsage>
{
    public void Configure(EntityTypeBuilder<ApiUsage> builder)
    {
        builder.ToTable("ApiUsage");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Service).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Endpoint).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Timestamp).IsRequired();
        builder.Property(x => x.Credits).IsRequired();
        builder.Property(x => x.StatusCode).IsRequired();
        builder.HasIndex(x => new { x.Service, x.Timestamp });
    }
}
