using PickArr.Core.Catalog;

namespace PickArr.Core.Recommendations;

public sealed record RecommendationsQuery(
    string Scope = RecoMembership.ScopeSource,
    int? SourceId = null,
    IReadOnlyList<string>? Types = null,
    IReadOnlyList<int>? Genres = null,
    int? YearFrom = null,
    int? YearTo = null,
    bool IncludeInLibrary = false,
    bool Diversify = true,
    int Page = 1,
    int PageSize = 48);

public sealed record RecommendationsPageDto(
    int Version,
    string Stage,
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<RecommendationItemDto> Items);

public sealed record RecommendationItemDto(
    int Id,
    string Name,
    string? OriginalTitle,
    int? Year,
    string Category,
    string? PosterUrl,
    IReadOnlyList<GenreRefDto> Genres,
    double? RatingTmdb,
    double? RatingImdb,
    bool IsFavorite,
    bool IsWatched,
    string? Rating,
    AvailabilitySummaryDto? Availability,
    ArrSummaryDto? Arr,
    IReadOnlyList<CatalogPlaceDto> Places,
    RecoItemScoreDto Reco);

public sealed record RecoItemScoreDto(
    double Score,
    bool Personalized,
    IReadOnlyList<RecoReasonDto> Reasons,
    double? Similarity = null);

public sealed record SimilarTitlesQuery(
    int TitleId,
    int? SourceId = null,
    int Limit = 20);

public sealed record SimilarTitlesDto(IReadOnlyList<RecommendationItemDto> Items);

public sealed record RecoReasonDto(
    string Kind,
    int? TitleId = null,
    string? Name = null,
    double? Similarity = null,
    IReadOnlyList<string>? Terms = null);

public sealed record RecoStatusDto(
    bool Enabled,
    string Stage,
    int RatingCount,
    double EffectiveN,
    double TargetN,
    double PersonalWeight,
    RecoModelStatusDto Model,
    RecoEmbeddingsStatusDto Embeddings,
    RecoPoolStatusDto Pool,
    DateTime? LastBuiltAt,
    int Version,
    string? LastError);

public sealed record RecoModelStatusDto(string Status, string? Id, long? SizeBytes);

public sealed record RecoEmbeddingsStatusDto(double Coverage, int Count, int Pending);

public sealed record RecoPoolStatusDto(int Titles);
