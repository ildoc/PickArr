using PickArr.Core.Recommendations;

namespace PickArr.Core.Abstractions;

public interface IRecoQueryService
{
    Task<RecommendationsPageDto> QueryAsync(
        RecommendationsQuery query,
        CancellationToken cancellationToken = default);

    Task<SimilarTitlesDto> QuerySimilarAsync(
        SimilarTitlesQuery query,
        CancellationToken cancellationToken = default);

    Task<RecommendationsPageDto> GetOnboardingAsync(
        int? sourceId = null,
        CancellationToken cancellationToken = default);

    Task<RecoStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
}
