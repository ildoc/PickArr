using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pickarr.Core.Domain.Entities;

namespace Pickarr.Infrastructure.Persistence.Configurations;

public sealed class ProfileConfiguration : IEntityTypeConfiguration<Profile>
{
    public void Configure(EntityTypeBuilder<Profile> builder)
    {
        builder.ToTable("Profiles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(Profile.MaxNameLength).IsRequired();
        builder.Property(x => x.Color).HasMaxLength(Profile.MaxColorLength).IsRequired();
        builder.Property(x => x.UseImplicit).HasDefaultValue(true);
        builder.Property(x => x.ExcludeInLibrary).HasDefaultValue(true);
        builder.HasIndex(x => x.IsAdmin)
            .IsUnique()
            .HasFilter("\"IsAdmin\" = 1");
    }
}
