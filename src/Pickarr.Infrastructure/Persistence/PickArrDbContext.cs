using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Domain.Entities;

namespace Pickarr.Infrastructure.Persistence;

public sealed class PickarrDbContext(DbContextOptions<PickarrDbContext> options) : DbContext(options)
{
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<CatalogSource> CatalogSources => Set<CatalogSource>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();
    public DbSet<ApiUsage> ApiUsages => Set<ApiUsage>();
    public DbSet<SyncRun> SyncRuns => Set<SyncRun>();
    public DbSet<Title> Titles => Set<Title>();
    public DbSet<TitleLocalization> TitleLocalizations => Set<TitleLocalization>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<GenreLocalization> GenreLocalizations => Set<GenreLocalization>();
    public DbSet<TitleGenre> TitleGenres => Set<TitleGenre>();
    public DbSet<Keyword> Keywords => Set<Keyword>();
    public DbSet<TitleKeyword> TitleKeywords => Set<TitleKeyword>();
    public DbSet<Person> People => Set<Person>();
    public DbSet<TitleCredit> TitleCredits => Set<TitleCredit>();
    public DbSet<Availability> Availabilities => Set<Availability>();
    public DbSet<AvailabilityEvent> AvailabilityEvents => Set<AvailabilityEvent>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<Watched> Watched => Set<Watched>();
    public DbSet<Rating> Ratings => Set<Rating>();
    public DbSet<RatingEvent> RatingEvents => Set<RatingEvent>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<RecoState> RecoStates => Set<RecoState>();
    public DbSet<RecoEventLog> RecoEventLogs => Set<RecoEventLog>();
    public DbSet<TitleEmbedding> TitleEmbeddings => Set<TitleEmbedding>();
    public DbSet<ArrLibraryItem> ArrLibraryItems => Set<ArrLibraryItem>();
    public DbSet<ArrSeasonState> ArrSeasonStates => Set<ArrSeasonState>();
    public DbSet<ArrAuditLog> ArrAuditLogs => Set<ArrAuditLog>();
    public DbSet<Profile> Profiles => Set<Profile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PickarrDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
