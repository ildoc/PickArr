namespace PickArr.Core.Settings;

public sealed record SecretStatus(bool IsSet);

public sealed class SettingsResponse
{
    public required WatchmodeSettingsDto Watchmode { get; init; }
    public required TmdbSettingsDto Tmdb { get; init; }
    public required OmdbSettingsDto Omdb { get; init; }
    public required ArrInstanceSettingsDto Sonarr { get; init; }
    public required ArrInstanceSettingsDto Radarr { get; init; }
    public required WebhookSettingsDto Webhooks { get; init; }
    public required UiSettingsDto Ui { get; init; }
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
    public required SecretStatus Token { get; init; }
}

public sealed class OmdbSettingsDto
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

public sealed class UpdateSettingsRequest
{
    public WatchmodeUpdateDto? Watchmode { get; init; }
    public TmdbUpdateDto? Tmdb { get; init; }
    public OmdbUpdateDto? Omdb { get; init; }
    public ArrInstanceUpdateDto? Sonarr { get; init; }
    public ArrInstanceUpdateDto? Radarr { get; init; }
    public UiSettingsDto? Ui { get; init; }
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
    public string? Token { get; init; }
}

public sealed class OmdbUpdateDto
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
