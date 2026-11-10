using Pickarr.Core.Metadata;

namespace Pickarr.Core.Abstractions;

public interface ISeriesGuide
{
    Task<IReadOnlyList<TmdbSeason>?> ListSeasonsAsync(
        int titleId,
        int? sourceId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TmdbEpisode>?> ListEpisodesAsync(
        int titleId,
        int seasonNumber,
        int? sourceId,
        CancellationToken cancellationToken = default);
}
