using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Arr;
using PickArr.Core.Catalog;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Infrastructure.Persistence;

namespace PickArr.Infrastructure.Arr;

public sealed class ArrAddService(
    PickArrDbContext db,
    IArrClientFactory clientFactory,
    IArrLibrarySync librarySync,
    ITitleQueryService titles,
    IClock clock) : IArrAddService
{
    public Task<ArrDetailDto> AddAsync(int titleId, SonarrAddOptions options, CancellationToken cancellationToken = default) =>
        AddCoreAsync(
            titleId,
            ArrKind.Sonarr,
            (object)options,
            (title, opts) =>
            {
                var sonarr = (SonarrAddOptions)opts;
                if (title.TvdbId is null && title.TmdbId is null && string.IsNullOrWhiteSpace(title.ImdbId))
                {
                    throw ArrOperationException.IdUnmapped("Series requires a TVDB, TMDB, or IMDb id.");
                }

                var seasons = sonarr.Seasons is { Count: > 0 }
                    ? sonarr.Seasons.Distinct().ToList()
                    : null;
                var monitor = seasons is null ? sonarr.Monitor : "skip";
                var monitored = seasons is not null
                    || !string.Equals(sonarr.Monitor, "none", StringComparison.OrdinalIgnoreCase);
                return new SonarrAddRequest(
                    title.TvdbId,
                    sonarr.QualityProfileId,
                    sonarr.RootFolderPath,
                    monitored,
                    sonarr.Tags,
                    monitor,
                    sonarr.MonitorNewItems,
                    sonarr.SeriesType,
                    sonarr.SeasonFolder,
                    sonarr.SearchNow,
                    title.TmdbId,
                    title.ImdbId,
                    seasons);
            },
            cancellationToken);

    public Task<ArrDetailDto> AddAsync(int titleId, RadarrAddOptions options, CancellationToken cancellationToken = default) =>
        AddCoreAsync(
            titleId,
            ArrKind.Radarr,
            (object)options,
            (title, opts) =>
            {
                var radarr = (RadarrAddOptions)opts;
                if (title.TmdbId is not { } tmdbId)
                {
                    throw ArrOperationException.IdUnmapped("Movie requires a TMDB id.");
                }

                return new RadarrAddRequest(
                    tmdbId,
                    radarr.QualityProfileId,
                    radarr.RootFolderPath,
                    radarr.Monitored,
                    radarr.Tags,
                    radarr.MinimumAvailability,
                    radarr.SearchNow);
            },
            cancellationToken);

    private async Task<ArrDetailDto> AddCoreAsync(
        int titleId,
        ArrKind expectedKind,
        object options,
        Func<Title, object, ArrAddRequest> buildRequest,
        CancellationToken cancellationToken)
    {
        var title = await db.Titles.AsNoTracking().FirstOrDefaultAsync(t => t.Id == titleId, cancellationToken)
                    ?? throw ArrOperationException.TitleNotFound(titleId);

        var kind = title.MediaType == MediaTypes.Series ? ArrKind.Sonarr : ArrKind.Radarr;
        if (kind != expectedKind)
        {
            throw ArrOperationException.Rejected(
                $"Title media type maps to {kind.ToApi()}, but {expectedKind.ToApi()} options were provided.");
        }

        if (kind == ArrKind.Sonarr
            && title.TvdbId is null
            && title.TmdbId is null
            && string.IsNullOrWhiteSpace(title.ImdbId))
        {
            throw ArrOperationException.IdUnmapped("Series requires a TVDB, TMDB, or IMDb id.");
        }

        if (kind == ArrKind.Radarr && title.TmdbId is null)
        {
            throw ArrOperationException.IdUnmapped("Movie requires a TMDB id.");
        }

        var client = await clientFactory.TryCreateAsync(kind, cancellationToken)
                     ?? throw ArrOperationException.NotConfigured();

        var externalId = new ArrExternalId(title.TmdbId, title.TvdbId, title.ImdbId);
        try
        {
            var existing = await client.FindByExternalIdAsync(externalId, cancellationToken);
            if (existing is not null)
            {
                await librarySync.UpsertItemAsync(kind, existing, cancellationToken);
                await WriteAuditAsync(
                    ArrAuditActions.Add,
                    kind,
                    existing.ArrId,
                    titleId,
                    options,
                    ArrAuditOutcomes.Failed,
                    "Already exists",
                    cancellationToken);
                throw ArrOperationException.AlreadyExists("Title is already present in the Arr library.");
            }

            var request = buildRequest(title, options);
            var created = await client.AddAsync(request, cancellationToken);
            if (title.TvdbId is null && created.TvdbId is { } learnedTvdb)
            {
                var tracked = await db.Titles.FirstAsync(t => t.Id == titleId, cancellationToken);
                tracked.TvdbId = learnedTvdb;
                tracked.UpdatedAt = clock.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }

            await librarySync.UpsertItemAsync(kind, created, cancellationToken);
            await WriteAuditAsync(
                ArrAuditActions.Add,
                kind,
                created.ArrId,
                titleId,
                options,
                ArrAuditOutcomes.Success,
                error: null,
                cancellationToken);

            var detail = await titles.GetTitleAsync(titleId, sourceId: null, cancellationToken);
            return detail?.Arr
                   ?? throw new InvalidOperationException("Title detail missing after Arr add.");
        }
        catch (ArrOperationException)
        {
            throw;
        }
        catch (ArrClientException ex)
        {
            await WriteAuditAsync(
                ArrAuditActions.Add,
                kind,
                arrId: null,
                titleId,
                options,
                ArrAuditOutcomes.Failed,
                ex.Message,
                cancellationToken);
            throw MapClientException(ex);
        }
        catch (HttpRequestException ex)
        {
            await WriteAuditAsync(
                ArrAuditActions.Add,
                kind,
                arrId: null,
                titleId,
                options,
                ArrAuditOutcomes.Failed,
                ex.Message,
                cancellationToken);
            throw ArrOperationException.Unreachable(ex.Message);
        }
    }

    private async Task WriteAuditAsync(
        string action,
        ArrKind kind,
        int? arrId,
        int titleId,
        object options,
        string outcome,
        string? error,
        CancellationToken cancellationToken)
    {
        db.ArrAuditLogs.Add(new ArrAuditLog
        {
            At = clock.UtcNow,
            Action = action,
            Kind = kind.ToStorage(),
            ArrId = arrId,
            TitleId = titleId,
            Options = JsonSerializer.Serialize(options, ArrJson.Options),
            Outcome = outcome,
            Error = error,
        });
        await db.SaveChangesAsync(cancellationToken);
    }

    internal static ArrOperationException MapClientException(ArrClientException ex)
    {
        if (ex.StatusCode is 401 or 403)
        {
            return ArrOperationException.Unauthorized(ex.Message);
        }

        if (ex.StatusCode is >= 500 or 0)
        {
            return ArrOperationException.Unreachable(ex.Message);
        }

        return ArrOperationException.Rejected(ex.Message, ex.Errors);
    }
}
