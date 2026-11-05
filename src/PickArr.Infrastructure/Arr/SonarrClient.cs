using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using PickArr.Core.Abstractions;
using PickArr.Core.Arr;
using PickArr.Core.Domain;
using PickArr.Core.Settings;

namespace PickArr.Infrastructure.Arr;

public sealed class SonarrClient(
    HttpClient http,
    ISettingsStore settings,
    ILogger<SonarrClient> logger) : IArrClient
{
    public ArrKind Kind => ArrKind.Sonarr;

    public async Task<ArrSystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = await RequireConfigAsync(cancellationToken);
        using var request = ArrHttp.Create(HttpMethod.Get, baseUrl, "system/status", apiKey);
        var dto = await ArrHttp.SendAsync<StatusDto>(http, request, logger, cancellationToken);
        return new ArrSystemInfo(dto.Version ?? "unknown", dto.AppName ?? "Sonarr");
    }

    public async Task<ArrOptions> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = await RequireConfigAsync(cancellationToken);
        var profiles = await GetListAsync<QualityProfileDto>(baseUrl, apiKey, "qualityprofile", cancellationToken);
        var roots = await GetListAsync<RootFolderDto>(baseUrl, apiKey, "rootfolder", cancellationToken);
        var tags = await GetListAsync<TagDto>(baseUrl, apiKey, "tag", cancellationToken);
        var defaults = await LoadDefaultsAsync(cancellationToken);

        var qualityProfiles = profiles.Select(p => new ArrQualityProfile(p.Id, p.Name ?? $"Profile {p.Id}")).ToList();
        var rootFolders = roots.Select(r => new ArrRootFolder(r.Path ?? "", r.FreeSpace)).ToList();
        var defaultProfile = defaults?.QualityProfileId
            ?? qualityProfiles.FirstOrDefault()?.Id;
        var defaultRoot = defaults?.RootFolderPath
            ?? rootFolders.FirstOrDefault()?.Path;

        return new ArrOptions(
            qualityProfiles,
            rootFolders,
            tags.Select(t => new ArrTag(t.Id, t.Label ?? t.Id.ToString())).ToList(),
            defaultProfile,
            defaultRoot);
    }

    public async Task<ArrConnectionProbe> ProbeAsync(
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        var normalized = baseUrl.Trim().TrimEnd('/');
        using var statusRequest = ArrHttp.Create(HttpMethod.Get, normalized, "system/status", apiKey);
        var status = await ArrHttp.SendAsync<StatusDto>(http, statusRequest, logger, cancellationToken);
        var profiles = await GetListAsync<QualityProfileDto>(normalized, apiKey, "qualityprofile", cancellationToken);
        var roots = await GetListAsync<RootFolderDto>(normalized, apiKey, "rootfolder", cancellationToken);
        return new ArrConnectionProbe(
            status.Version ?? "unknown",
            status.AppName ?? "Sonarr",
            profiles.Select(p => new ArrQualityProfile(p.Id, p.Name ?? $"Profile {p.Id}")).ToList(),
            roots.Select(r => new ArrRootFolder(r.Path ?? "", r.FreeSpace)).ToList());
    }

    public async Task<IReadOnlyList<ArrItemSnapshot>> GetLibraryAsync(CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = await RequireConfigAsync(cancellationToken);
        var series = await GetListAsync<SeriesDto>(baseUrl, apiKey, "series", cancellationToken);
        return series.Select(MapSeries).ToList();
    }

    public async Task<IReadOnlyList<ArrQueueEntry>> GetQueueAsync(CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = await RequireConfigAsync(cancellationToken);
        var entries = new List<ArrQueueEntry>();
        var page = 1;
        while (true)
        {
            using var request = ArrHttp.Create(
                HttpMethod.Get,
                baseUrl,
                $"queue?page={page}&pageSize=100&includeSeries=true",
                apiKey);
            var dto = await ArrHttp.SendAsync<QueuePageDto>(http, request, logger, cancellationToken);
            foreach (var record in dto.Records ?? [])
            {
                if (record.SeriesId is { } seriesId)
                {
                    entries.Add(new ArrQueueEntry(seriesId, record.Size, record.Sizeleft, record.Status));
                }
            }

            if (dto.Records is null || dto.Records.Count == 0 || entries.Count >= dto.TotalRecords)
            {
                break;
            }

            page++;
            if (page > 100)
            {
                break;
            }
        }

        return entries;
    }

    public async Task<ArrItemSnapshot?> FindByExternalIdAsync(ArrExternalId id, CancellationToken cancellationToken = default)
    {
        var tvdbId = id.TvdbId ?? await ResolveTvdbIdAsync(id.TmdbId, id.ImdbId, cancellationToken);
        if (tvdbId is not { } resolved)
        {
            return null;
        }

        var (baseUrl, apiKey) = await RequireConfigAsync(cancellationToken);
        var series = await GetListAsync<SeriesDto>(baseUrl, apiKey, $"series?tvdbId={resolved}", cancellationToken);
        return series.Select(MapSeries).FirstOrDefault();
    }

    public async Task<ArrItemSnapshot> AddAsync(ArrAddRequest request, CancellationToken cancellationToken = default)
    {
        if (request is not SonarrAddRequest sonarr)
        {
            throw new ArgumentException("Expected SonarrAddRequest.", nameof(request));
        }

        var term = SeriesLookupTerm(sonarr.TvdbId, sonarr.TmdbId, sonarr.ImdbId)
                   ?? throw new ArrClientException(422, "Series requires a TVDB, TMDB, or IMDb id.");
        var (baseUrl, apiKey) = await RequireConfigAsync(cancellationToken);
        var candidates = await LookupSeriesAsync(baseUrl, apiKey, term, cancellationToken);
        if (candidates.Count == 0)
        {
            throw new ArrClientException(424, $"Sonarr lookup returned no results for {term}");
        }

        var payload = JsonNode.Parse(candidates[0].GetRawText())?.AsObject()
                      ?? throw new ArrClientException(424, "Invalid Sonarr lookup payload.");
        payload["qualityProfileId"] = sonarr.QualityProfileId;
        payload["rootFolderPath"] = sonarr.RootFolderPath;
        payload["seasonFolder"] = sonarr.SeasonFolder;
        payload["seriesType"] = sonarr.SeriesType;
        payload["monitorNewItems"] = sonarr.MonitorNewItems;
        payload["tags"] = JsonSerializer.SerializeToNode(sonarr.Tags, ArrJson.Options);
        var monitor = sonarr.Monitor;
        var monitored = sonarr.Monitored;
        if (sonarr.Seasons is { Count: > 0 })
        {
            ApplySeasonSelection(payload, sonarr.Seasons);
            monitor = "skip";
            monitored = true;
        }

        payload["monitored"] = monitored;
        payload["addOptions"] = new JsonObject
        {
            ["monitor"] = monitor,
            ["searchForMissingEpisodes"] = sonarr.SearchForMissingEpisodes,
            ["searchForCutoffUnmetEpisodes"] = false,
        };

        using var post = ArrHttp.Create(HttpMethod.Post, baseUrl, "series", apiKey);
        post.Content = new StringContent(payload.ToJsonString(ArrJson.Options), System.Text.Encoding.UTF8, "application/json");
        var created = await ArrHttp.SendAsync<SeriesDto>(http, post, logger, cancellationToken);
        return MapSeries(created);
    }

    public async Task SearchAsync(int arrId, CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = await RequireConfigAsync(cancellationToken);
        using var request = ArrHttp.Create(HttpMethod.Post, baseUrl, "command", apiKey);
        request.Content = JsonContent.Create(new { name = "SeriesSearch", seriesId = arrId }, options: ArrJson.Options);
        await ArrHttp.SendAsync(http, request, logger, cancellationToken);
    }

    public async Task DeleteAsync(int arrId, bool deleteFiles, bool addImportExclusion, CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = await RequireConfigAsync(cancellationToken);
        using var request = ArrHttp.Create(
            HttpMethod.Delete,
            baseUrl,
            $"series/{arrId}?deleteFiles={deleteFiles.ToString().ToLowerInvariant()}&addImportExclusion={addImportExclusion.ToString().ToLowerInvariant()}",
            apiKey);
        await ArrHttp.SendAsync(http, request, logger, cancellationToken);
    }

    private async Task<(string Url, string ApiKey)> RequireConfigAsync(CancellationToken cancellationToken)
    {
        var config = await ArrHttp.ResolveConfigAsync(
            settings,
            SettingsKeys.SonarrUrl,
            SettingsKeys.SonarrApiKey,
            cancellationToken);
        return config ?? throw new InvalidOperationException("Sonarr is not configured.");
    }

    private async Task<List<T>> GetListAsync<T>(string baseUrl, string apiKey, string path, CancellationToken cancellationToken)
    {
        using var request = ArrHttp.Create(HttpMethod.Get, baseUrl, path, apiKey);
        return await ArrHttp.SendAsync<List<T>>(http, request, logger, cancellationToken);
    }

    private async Task<ArrDefaults?> LoadDefaultsAsync(CancellationToken cancellationToken)
    {
        var raw = await settings.GetRawAsync(SettingsKeys.SonarrDefaults, cancellationToken);
        return string.IsNullOrWhiteSpace(raw) ? null : System.Text.Json.JsonSerializer.Deserialize<ArrDefaults>(raw, ArrJson.Options);
    }

    internal static string? SeriesLookupTerm(int? tvdbId, int? tmdbId, string? imdbId)
    {
        if (tvdbId is > 0)
        {
            return $"tvdb:{tvdbId.Value}";
        }

        if (tmdbId is > 0)
        {
            return $"tmdb:{tmdbId.Value}";
        }

        if (!string.IsNullOrWhiteSpace(imdbId))
        {
            return $"imdb:{imdbId.Trim()}";
        }

        return null;
    }

    private async Task<int?> ResolveTvdbIdAsync(int? tmdbId, string? imdbId, CancellationToken cancellationToken)
    {
        var term = SeriesLookupTerm(null, tmdbId, imdbId);
        if (term is null)
        {
            return null;
        }

        var (baseUrl, apiKey) = await RequireConfigAsync(cancellationToken);
        var candidates = await LookupSeriesAsync(baseUrl, apiKey, term, cancellationToken);
        if (candidates.Count == 0 || !candidates[0].TryGetProperty("tvdbId", out var tvdb) || !tvdb.TryGetInt32(out var id))
        {
            return null;
        }

        return id;
    }

    private async Task<List<JsonElement>> LookupSeriesAsync(
        string baseUrl,
        string apiKey,
        string term,
        CancellationToken cancellationToken)
    {
        using var lookupRequest = ArrHttp.Create(
            HttpMethod.Get,
            baseUrl,
            $"series/lookup?term={Uri.EscapeDataString(term)}",
            apiKey);
        return await ArrHttp.SendAsync<List<JsonElement>>(http, lookupRequest, logger, cancellationToken);
    }

    internal static void ApplySeasonSelection(JsonObject payload, IReadOnlyList<int> selectedSeasons)
    {
        var selected = selectedSeasons.ToHashSet();
        if (payload["seasons"] is not JsonArray seasons)
        {
            seasons = [];
            payload["seasons"] = seasons;
        }

        var present = new HashSet<int>();
        foreach (var node in seasons)
        {
            if (node is not JsonObject season)
            {
                continue;
            }

            var number = ReadSeasonNumber(season);
            present.Add(number);
            season["monitored"] = selected.Contains(number);
        }

        foreach (var number in selected.Order())
        {
            if (present.Contains(number))
            {
                continue;
            }

            seasons.Add(new JsonObject
            {
                ["seasonNumber"] = number,
                ["monitored"] = true,
            });
        }
    }

    private static int ReadSeasonNumber(JsonObject season)
    {
        if (season["seasonNumber"] is JsonValue value && value.TryGetValue<int>(out var number))
        {
            return number;
        }

        return -1;
    }

    private static ArrItemSnapshot MapSeries(SeriesDto s)
    {
        var seasons = (s.Seasons ?? [])
            .Select(season => new ArrSeasonSnapshot(
                season.SeasonNumber,
                season.Monitored,
                season.Statistics?.EpisodeFileCount ?? 0,
                season.Statistics?.EpisodeCount ?? 0,
                season.Statistics?.TotalEpisodeCount ?? 0,
                season.Statistics?.SizeOnDisk ?? 0))
            .ToList();
        return new ArrItemSnapshot(
            s.Id,
            s.Title ?? $"Series {s.Id}",
            s.Year,
            s.TmdbId,
            s.TvdbId,
            s.ImdbId,
            s.Monitored,
            s.Path,
            s.Statistics?.SizeOnDisk ?? 0,
            HasFile: null,
            IsAvailable: null,
            s.Statistics?.EpisodeFileCount,
            s.Statistics?.EpisodeCount,
            s.Statistics?.TotalEpisodeCount,
            seasons);
    }

    private sealed record StatusDto(string? Version, string? AppName);
    private sealed record QualityProfileDto(int Id, string? Name);
    private sealed record RootFolderDto(string? Path, long? FreeSpace);
    private sealed record TagDto(int Id, string? Label);
    private sealed record ArrDefaults(int? QualityProfileId, string? RootFolderPath);
    private sealed record SeriesDto(
        int Id,
        string? Title,
        int? Year,
        int? TvdbId,
        int? TmdbId,
        string? ImdbId,
        string? Path,
        bool Monitored,
        SeriesStatsDto? Statistics,
        List<SeasonDto>? Seasons);
    private sealed record SeriesStatsDto(int EpisodeFileCount, int EpisodeCount, int TotalEpisodeCount, long SizeOnDisk);
    private sealed record SeasonDto(int SeasonNumber, bool Monitored, SeriesStatsDto? Statistics);
    private sealed record QueuePageDto(int Page, int PageSize, int TotalRecords, List<QueueRecordDto>? Records);
    private sealed record QueueRecordDto(
        int? SeriesId,
        double Size,
        [property: JsonPropertyName("sizeleft")] double Sizeleft,
        string? Status);
}
