using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Jobs;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Settings;

public sealed class SetupService(
    SettingsService settings,
    PickarrDbContext db,
    CatalogBootstrap bootstrap)
{
    public async Task<SetupStatusDto> GetStatusAsync(CancellationToken cancellationToken = default) =>
        new() { Completed = await settings.IsSetupCompletedAsync(cancellationToken) };

    public async Task<(SetupStatusDto? Status, string? Error)> CompleteAsync(
        CancellationToken cancellationToken = default)
    {
        if (await settings.IsSetupCompletedAsync(cancellationToken))
        {
            return (await GetStatusAsync(cancellationToken), null);
        }

        var current = await settings.GetAsync(cancellationToken);
        if (!current.Watchmode.ApiKey.IsSet)
        {
            return (null, "A Watchmode API key is required.");
        }

        if (!current.Tmdb.ApiKey.IsSet)
        {
            return (null, "A TMDB API key is required.");
        }

        var hasCatalog = await db.CatalogSources.AsNoTracking().AnyAsync(
            source => source.IsEnabled && source.Provider != null && source.Provider.IsEnabled,
            cancellationToken);
        if (!hasCatalog)
        {
            return (null, "Enable at least one provider and one country.");
        }

        await settings.MarkSetupCompletedAsync(cancellationToken);
        await bootstrap.StartAsync(force: true, cancellationToken);
        return (await GetStatusAsync(cancellationToken), null);
    }
}
