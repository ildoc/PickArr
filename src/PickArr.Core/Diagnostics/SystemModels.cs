namespace Pickarr.Core.Diagnostics;

public sealed record SystemStatsDto(
    long TotalTitles,
    IReadOnlyList<NamedTitleCountDto> ByProvider,
    IReadOnlyList<NamedTitleCountDto> ByCountry,
    IReadOnlyList<SourceTitleCountDto> BySource,
    SystemStorageDto Storage);

public sealed record NamedTitleCountDto(string Name, long TitleCount);

public sealed record SourceTitleCountDto(string ProviderName, string CountryCode, long TitleCount);

public sealed record SystemStorageDto(
    long TotalBytes,
    IReadOnlyList<StorageEntryDto> Entries);

public sealed record StorageEntryDto(string Key, string? RelativePath, long Bytes);
