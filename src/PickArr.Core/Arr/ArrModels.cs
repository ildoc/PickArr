using Pickarr.Core.Domain;

namespace Pickarr.Core.Arr;

public sealed record ArrSystemInfo(string Version, string AppName);

public sealed record ArrQualityProfile(int Id, string Name);

public sealed record ArrRootFolder(string Path, long? FreeSpace);

public sealed record ArrTag(int Id, string Label);

public sealed record ArrOptions(
    IReadOnlyList<ArrQualityProfile> QualityProfiles,
    IReadOnlyList<ArrRootFolder> RootFolders,
    IReadOnlyList<ArrTag> Tags,
    int? DefaultQualityProfileId,
    string? DefaultRootFolderPath);

/// <summary>
/// Result of testing an Arr server with explicit URL and API key, including the lists needed to pick defaults.
/// </summary>
public sealed record ArrConnectionProbe(
    string Version,
    string AppName,
    IReadOnlyList<ArrQualityProfile> QualityProfiles,
    IReadOnlyList<ArrRootFolder> RootFolders);

public sealed record ArrSeasonSnapshot(
    int SeasonNumber,
    bool Monitored,
    int EpisodeFileCount,
    int EpisodeCount,
    int TotalEpisodeCount,
    long SizeOnDisk);

public sealed record ArrItemSnapshot(
    int ArrId,
    string Title,
    int? Year,
    int? TmdbId,
    int? TvdbId,
    string? ImdbId,
    bool Monitored,
    string? Path,
    long SizeOnDisk,
    bool? HasFile,
    bool? IsAvailable,
    int? EpisodeFileCount,
    int? EpisodeCount,
    int? TotalEpisodeCount,
    IReadOnlyList<ArrSeasonSnapshot> Seasons);

public sealed record ArrQueueEntry(
    int ArrItemId,
    double Size,
    double SizeLeft,
    string? Status);

public sealed record ArrExternalId(int? TmdbId, int? TvdbId, string? ImdbId);

public abstract record ArrAddRequest(int QualityProfileId, string RootFolderPath, bool Monitored, IReadOnlyList<int> Tags);

public sealed record SonarrAddRequest(
    int? TvdbId,
    int QualityProfileId,
    string RootFolderPath,
    bool Monitored,
    IReadOnlyList<int> Tags,
    string Monitor,
    string MonitorNewItems,
    string SeriesType,
    bool SeasonFolder,
    bool SearchForMissingEpisodes,
    int? TmdbId = null,
    string? ImdbId = null,
    IReadOnlyList<int>? Seasons = null) : ArrAddRequest(QualityProfileId, RootFolderPath, Monitored, Tags);

public sealed record RadarrAddRequest(
    int TmdbId,
    int QualityProfileId,
    string RootFolderPath,
    bool Monitored,
    IReadOnlyList<int> Tags,
    string MinimumAvailability,
    bool SearchForMovie) : ArrAddRequest(QualityProfileId, RootFolderPath, Monitored, Tags);

public static class ArrKindExtensions
{
    public static string ToStorage(this ArrKind kind) => kind switch
    {
        ArrKind.Sonarr => ArrKinds.Sonarr,
        ArrKind.Radarr => ArrKinds.Radarr,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string ToApi(this ArrKind kind) => kind.ToStorage().ToLowerInvariant();

    public static string ToApi(this ArrState state) => state switch
    {
        ArrState.NotInLibrary => "notInLibrary",
        ArrState.Missing => "missing",
        ArrState.Downloading => "downloading",
        ArrState.Downloaded => "downloaded",
        ArrState.Partial => "partial",
        _ => state.ToString().ToLowerInvariant(),
    };

    public static ArrState ParseState(string value) => value.ToLowerInvariant() switch
    {
        "notinlibrary" => ArrState.NotInLibrary,
        "missing" => ArrState.Missing,
        "downloading" => ArrState.Downloading,
        "downloaded" => ArrState.Downloaded,
        "partial" => ArrState.Partial,
        _ => Enum.TryParse<ArrState>(value, ignoreCase: true, out var s) ? s : ArrState.Missing,
    };
}
