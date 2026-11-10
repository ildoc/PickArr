using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pickarr.Core.Domain.Entities;

namespace Pickarr.Infrastructure.Persistence.Configurations;

public sealed class KeywordConfiguration : IEntityTypeConfiguration<Keyword>
{
    public void Configure(EntityTypeBuilder<Keyword> builder)
    {
        builder.ToTable("Keywords");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(128).IsRequired();
    }
}

public sealed class TitleKeywordConfiguration : IEntityTypeConfiguration<TitleKeyword>
{
    public void Configure(EntityTypeBuilder<TitleKeyword> builder)
    {
        builder.ToTable("TitleKeywords");
        builder.HasKey(x => new { x.TitleId, x.KeywordId });
        builder.HasOne(x => x.Title)
            .WithMany(x => x.TitleKeywords)
            .HasForeignKey(x => x.TitleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Keyword)
            .WithMany(x => x.TitleKeywords)
            .HasForeignKey(x => x.KeywordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("People");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Name).HasMaxLength(256).IsRequired();
    }
}

public sealed class TitleCreditConfiguration : IEntityTypeConfiguration<TitleCredit>
{
    public void Configure(EntityTypeBuilder<TitleCredit> builder)
    {
        builder.ToTable("TitleCredits");
        builder.HasKey(x => new { x.TitleId, x.PersonId, x.Role });
        builder.Property(x => x.Role).HasMaxLength(32).IsRequired();
        builder.HasOne(x => x.Title)
            .WithMany(x => x.TitleCredits)
            .HasForeignKey(x => x.TitleId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Person)
            .WithMany(x => x.TitleCredits)
            .HasForeignKey(x => x.PersonId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
