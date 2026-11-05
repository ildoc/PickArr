using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;
using PickArr.Core.Metadata;
using PickArr.Infrastructure.Persistence;

namespace PickArr.Infrastructure.Metadata;

public sealed class SeriesGuideService(PickArrDbContext db, IMetadataProvider metadata) : ISeriesGuide
{
    public async Task<IReadOnlyList<TmdbSeason>?> ListSeasonsAsync(
        int titleId,
        int? sourceId,
        CancellationToken cancellationToken = default)
    {
        var title = await db.Titles.FirstOrDefaultAsync(t => t.Id == titleId, cancellationToken);
        if (title is null)
        {
            return null;
        }

        if (title.MediaType != MediaTypes.Series || title.TmdbId is not int tmdbId)
        {
            return [];
        }

        var seasons = SeriesSeasonJson.Read(title.SeasonsJson);
        if (seasons.Count == 0)
        {
            var language = await ResolveLanguageAsync(title.Id, sourceId, cancellationToken);
            var details = await metadata.GetTvAsync(tmdbId, language, cancellationToken);
            seasons = details?.Seasons?.ToList() ?? [];
            if (seasons.Count > 0)
            {
                title.SeasonsJson = SeriesSeasonJson.Write(seasons);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return seasons.OrderByDescending(season => season.Number).ToList();
    }

    public async Task<IReadOnlyList<TmdbEpisode>?> ListEpisodesAsync(
        int titleId,
        int seasonNumber,
        int? sourceId,
        CancellationToken cancellationToken = default)
    {
        var title = await db.Titles.FirstOrDefaultAsync(t => t.Id == titleId, cancellationToken);
        if (title is null)
        {
            return null;
        }

        if (title.MediaType != MediaTypes.Series || title.TmdbId is not int tmdbId)
        {
            return [];
        }

        var seasons = SeriesSeasonJson.Read(title.SeasonsJson);
        var cached = seasons.FirstOrDefault(season => season.Number == seasonNumber)?.Episodes;
        if (cached is { Count: > 0 })
        {
            return cached.OrderBy(episode => episode.Number).ToList();
        }

        var language = await ResolveLanguageAsync(title.Id, sourceId, cancellationToken);
        var episodes = await metadata.GetTvSeasonAsync(tmdbId, seasonNumber, language, cancellationToken);
        var ordered = episodes.OrderBy(episode => episode.Number).ToList();
        title.SeasonsJson = SeriesSeasonJson.Write(
            SeriesSeasonJson.WithEpisodes(seasons, seasonNumber, ordered));
        await db.SaveChangesAsync(cancellationToken);
        return ordered;
    }

    private async Task<string> ResolveLanguageAsync(
        int titleId,
        int? sourceId,
        CancellationToken cancellationToken)
    {
        if (sourceId is int sid)
        {
            var language = await db.CatalogSources.AsNoTracking()
                .Where(source => source.Id == sid)
                .Select(source => source.MetadataLanguage)
                .FirstOrDefaultAsync(cancellationToken);
            if (!string.IsNullOrWhiteSpace(language))
            {
                return language;
            }
        }

        var fromAvailability = await db.Availabilities.AsNoTracking()
            .Where(availability => availability.TitleId == titleId && availability.CatalogSource != null)
            .Select(availability => availability.CatalogSource!.MetadataLanguage)
            .FirstOrDefaultAsync(cancellationToken);
        return string.IsNullOrWhiteSpace(fromAvailability) ? LanguageFallback.English : fromAvailability;
    }
}
