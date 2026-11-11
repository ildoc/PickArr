using Pickarr.Core.Abstractions;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Catalog;
using Pickarr.Infrastructure.Jobs;

namespace Pickarr.Api.Endpoints;

public static class SettingsEndpoints
{
    public static IEndpointRouteBuilder MapSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/settings");

        group.MapGet("/", async (SettingsService settings, CancellationToken ct) =>
            Results.Ok(await settings.GetAsync(ct)));

        group.MapPut("/", async (
            UpdateSettingsRequest request,
            SettingsService settings,
            CatalogBootstrap bootstrap,
            ISyncRunStore syncRuns,
            CancellationToken ct) =>
        {
            await settings.UpdateAsync(request, ct);
            if (request.Jobs?.RunHistoryLimit is not null)
            {
                await syncRuns.PruneHistoryAsync(ct);
            }

            await bootstrap.StartAsync(force: false, ct);
            return Results.Ok(await settings.GetAsync(ct));
        });

        group.MapGet("/catalogs", async (CatalogConfigurationService catalogs, CancellationToken ct) =>
            Results.Ok(await catalogs.GetAsync(ct)));

        group.MapPut("/catalogs", async (
            UpdateCatalogSetupRequest request,
            CatalogConfigurationService catalogs,
            CancellationToken ct) =>
        {
            try
            {
                return Results.Ok(await catalogs.UpdateAsync(request, ct));
            }
            catch (CatalogSetupException ex)
            {
                return Results.BadRequest(new { detail = ex.Message });
            }
        });

        return endpoints;
    }
}
