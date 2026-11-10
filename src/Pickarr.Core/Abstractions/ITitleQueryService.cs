using Pickarr.Core.Catalog;

namespace Pickarr.Core.Abstractions;

public interface ITitleQueryService
{
    Task<IReadOnlyList<CatalogSourceDto>> ListSourcesAsync(CancellationToken cancellationToken = default);

    Task<PagedTitlesResult> QueryTitlesAsync(
        int sourceId,
        TitleQueryRequest request,
        CancellationToken cancellationToken = default,
        int? personalProfileId = null);

    Task<IReadOnlyList<GenreCountDto>> ListGenresAsync(
        int sourceId,
        bool favoritesOnly,
        CancellationToken cancellationToken = default,
        int? personalProfileId = null);

    Task<IReadOnlyList<CatalogTypeCountDto>> ListTypesAsync(
        int sourceId,
        bool favoritesOnly,
        CancellationToken cancellationToken = default,
        int? personalProfileId = null);

    Task<TitleDetailDto?> GetTitleAsync(
        int titleId,
        int? sourceId,
        CancellationToken cancellationToken = default,
        int? personalProfileId = null);

    Task<PagedTitlesResult> QueryFavoritesAsync(
        int profileId,
        TitleQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedTitlesResult> QueryRatingsAsync(
        int profileId,
        string ratingValue,
        TitleQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GenreCountDto>> ListFavoriteGenresAsync(
        int profileId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogTypeCountDto>> ListFavoriteTypesAsync(
        int profileId,
        CancellationToken cancellationToken = default);
}
