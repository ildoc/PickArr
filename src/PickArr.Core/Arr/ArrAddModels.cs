namespace Pickarr.Core.Arr;

public sealed record SonarrAddOptions(
    int QualityProfileId,
    string RootFolderPath,
    string Monitor,
    string MonitorNewItems,
    string SeriesType,
    bool SeasonFolder,
    IReadOnlyList<int> Tags,
    bool SearchNow,
    IReadOnlyList<int>? Seasons = null);

public sealed record RadarrAddOptions(
    int QualityProfileId,
    string RootFolderPath,
    bool Monitored,
    string MinimumAvailability,
    IReadOnlyList<int> Tags,
    bool SearchNow);
