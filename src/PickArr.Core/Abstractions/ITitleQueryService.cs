using PickArr.Core.Catalog;

namespace PickArr.Core.Abstractions;

public interface ITitleQueryService
{
    Task<IReadOnlyList<CatalogSourceDto>> ListSourcesAsync(CancellationToken cancellationToken = default);

    Task<PagedTitlesResult> QueryTitlesAsync(
        int sourceId,
        TitleQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GenreCountDto>> ListGenresAsync(
        int sourceId,
        bool favoritesOnly,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogTypeCountDto>> ListTypesAsync(
        int sourceId,
        bool favoritesOnly,
        CancellationToken cancellationToken = default);

    Task<TitleDetailDto?> GetTitleAsync(
        int titleId,
        int? sourceId,
        CancellationToken cancellationToken = default);

    Task<PagedTitlesResult> QueryFavoritesAsync(
        TitleQueryRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GenreCountDto>> ListFavoriteGenresAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CatalogTypeCountDto>> ListFavoriteTypesAsync(
        CancellationToken cancellationToken = default);
}
