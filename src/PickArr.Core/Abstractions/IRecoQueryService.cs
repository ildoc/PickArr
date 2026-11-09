using Pickarr.Core.Recommendations;

namespace Pickarr.Core.Abstractions;

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
        IReadOnlyList<int>? excludeTitleIds = null,
        CancellationToken cancellationToken = default);

    Task<RecoStatusDto> GetStatusAsync(CancellationToken cancellationToken = default);
}
