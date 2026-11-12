namespace Pickarr.Core.Settings;

public sealed record SecretStatus(bool IsSet, bool FromEnvironment = false);

public sealed class SettingsResponse
{
    public required WatchmodeSettingsDto Watchmode { get; init; }
    public required TmdbSettingsDto Tmdb { get; init; }
    public required ArrInstanceSettingsDto Sonarr { get; init; }
    public required ArrInstanceSettingsDto Radarr { get; init; }
    public required WebhookSettingsDto Webhooks { get; init; }
    public required UiSettingsDto Ui { get; init; }
    public required RecoSettingsDto Reco { get; init; }
    public required JobsSettingsDto Jobs { get; init; }

    /// <summary>Canonical setting keys whose effective value comes from process configuration.</summary>
    public required IReadOnlyList<string> EnvironmentKeys { get; init; }
}

public sealed class SetupStatusDto
{
    public required bool Completed { get; init; }
}

public sealed class ConnectionTestRequest
{
    public required string Service { get; init; }
    public string? ApiKey { get; init; }
}

public sealed class ConnectionTestResponse
{
    public required bool Ok { get; init; }
    public string? Detail { get; init; }
}

public sealed class JobsSettingsDto
{
    /// <summary>Finished SyncRuns kept per job (Kind + CatalogSourceId). Clamped 1–100.</summary>
    public required int RunHistoryLimit { get; init; }
}

public sealed class WatchmodeSettingsDto
{
    public required SecretStatus ApiKey { get; init; }
    public required string Plan { get; init; }
    public required int Quota { get; init; }
    public required int ReservePercent { get; init; }
    public required int MaxCreditsPerRun { get; init; }
}

public sealed class TmdbSettingsDto
{
    public required SecretStatus ApiKey { get; init; }
}

public sealed class ArrInstanceSettingsDto
{
    public string? Url { get; init; }
    public required SecretStatus ApiKey { get; init; }
    public int SyncIntervalMinutes { get; init; } = 15;
    public ArrDefaultsDto? Defaults { get; init; }
}

public sealed class ArrDefaultsDto
{
    public int? QualityProfileId { get; init; }
    public string? RootFolderPath { get; init; }
}

public sealed class WebhookSettingsDto
{
    public required SecretStatus Token { get; init; }
}

public sealed class UiSettingsDto
{
    public required string Language { get; init; }
}

public sealed class RecoSettingsDto
{
    public required bool Enabled { get; init; }
    public required bool UseImplicit { get; init; }
    public required bool EmbeddingsEnabled { get; init; }
    public required bool ExcludeInLibrary { get; init; }
    public required int N0 { get; init; }
    public required double WeightLove { get; init; }
    public required double WeightLike { get; init; }
    public required double WeightDislike { get; init; }
    public required double WeightNotInterested { get; init; }
    public required double WeightFavorite { get; init; }
    public required double WeightArrAdd { get; init; }
    public required double WeightDownloaded { get; init; }
    public required double WeightWatched { get; init; }
    public required double MmrLambda { get; init; }
    public required double ExploreRate { get; init; }
    public required int MlMinRatings { get; init; }
    public required bool EventsEnabled { get; init; }
}

public sealed class UpdateSettingsRequest
{
    public WatchmodeUpdateDto? Watchmode { get; init; }
    public TmdbUpdateDto? Tmdb { get; init; }
    public ArrInstanceUpdateDto? Sonarr { get; init; }
    public ArrInstanceUpdateDto? Radarr { get; init; }
    public UiSettingsDto? Ui { get; init; }
    public RecoUpdateDto? Reco { get; init; }
    public JobsUpdateDto? Jobs { get; init; }
}

public sealed class JobsUpdateDto
{
    public int? RunHistoryLimit { get; init; }
}

public sealed class RecoUpdateDto
{
    public bool? Enabled { get; init; }
    public bool? UseImplicit { get; init; }
    public bool? EmbeddingsEnabled { get; init; }
    public bool? ExcludeInLibrary { get; init; }
    public int? N0 { get; init; }
    public double? WeightLove { get; init; }
    public double? WeightLike { get; init; }
    public double? WeightDislike { get; init; }
    public double? WeightNotInterested { get; init; }
    public double? WeightFavorite { get; init; }
    public double? WeightArrAdd { get; init; }
    public double? WeightDownloaded { get; init; }
    public double? WeightWatched { get; init; }
    public double? MmrLambda { get; init; }
    public double? ExploreRate { get; init; }
    public int? MlMinRatings { get; init; }
    public bool? EventsEnabled { get; init; }
}

public sealed class WatchmodeUpdateDto
{
    public string? ApiKey { get; init; }
    public string? Plan { get; init; }
    public int? Quota { get; init; }
    public int? ReservePercent { get; init; }
    public int? MaxCreditsPerRun { get; init; }
}

public sealed class TmdbUpdateDto
{
    public string? ApiKey { get; init; }
}

public sealed class ArrInstanceUpdateDto
{
    public string? Url { get; init; }
    public string? ApiKey { get; init; }
    public int? SyncIntervalMinutes { get; init; }
    public ArrDefaultsDto? Defaults { get; init; }
}
