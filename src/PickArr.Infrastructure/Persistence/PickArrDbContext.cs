using Microsoft.EntityFrameworkCore;
using PickArr.Core.Domain.Entities;

namespace PickArr.Infrastructure.Persistence;

public sealed class PickArrDbContext(DbContextOptions<PickArrDbContext> options) : DbContext(options)
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
    public DbSet<Availability> Availabilities => Set<Availability>();
    public DbSet<AvailabilityEvent> AvailabilityEvents => Set<AvailabilityEvent>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<ArrLibraryItem> ArrLibraryItems => Set<ArrLibraryItem>();
    public DbSet<ArrSeasonState> ArrSeasonStates => Set<ArrSeasonState>();
    public DbSet<ArrAuditLog> ArrAuditLogs => Set<ArrAuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PickArrDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
