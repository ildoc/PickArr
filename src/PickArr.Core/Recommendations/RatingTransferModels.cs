namespace Pickarr.Core.Recommendations;

public sealed record RatingExportItemDto(
    string TmdbType,
    int TmdbId,
    string Name,
    string Value,
    DateTime RatedAt);

public sealed record RatingImportRequest(
    string Mode,
    IReadOnlyList<RatingImportItemDto> Items);

public sealed record RatingImportItemDto(
    string TmdbType,
    int TmdbId,
    string Value);

public sealed record RatingImportResultDto(
    int Imported,
    int Unknown,
    int Ignored);
