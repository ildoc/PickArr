using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Arr;
using Pickarr.Core.Domain;
using Pickarr.Core.Settings;

namespace Pickarr.Infrastructure.Arr;

public sealed class RadarrClient(
    HttpClient http,
    ISettingsStore settings,
    ILogger<RadarrClient> logger) : IArrClient
{
    public ArrKind Kind => ArrKind.Radarr;

    public async Task<ArrSystemInfo> GetSystemInfoAsync(CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = await RequireConfigAsync(cancellationToken);
        using var request = ArrHttp.Create(HttpMethod.Get, baseUrl, "system/status", apiKey);
        var dto = await ArrHttp.SendAsync<StatusDto>(http, request, logger, cancellationToken);
        return new ArrSystemInfo(dto.Version ?? "unknown", dto.AppName ?? "Radarr");
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
        return new ArrOptions(
            qualityProfiles,
            rootFolders,
            tags.Select(t => new ArrTag(t.Id, t.Label ?? t.Id.ToString())).ToList(),
            defaults?.QualityProfileId ?? qualityProfiles.FirstOrDefault()?.Id,
            defaults?.RootFolderPath ?? rootFolders.FirstOrDefault()?.Path);
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
            status.AppName ?? "Radarr",
            profiles.Select(p => new ArrQualityProfile(p.Id, p.Name ?? $"Profile {p.Id}")).ToList(),
            roots.Select(r => new ArrRootFolder(r.Path ?? "", r.FreeSpace)).ToList());
    }

    public async Task<IReadOnlyList<ArrItemSnapshot>> GetLibraryAsync(CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = await RequireConfigAsync(cancellationToken);
        var movies = await GetListAsync<MovieDto>(baseUrl, apiKey, "movie", cancellationToken);
        return movies.Select(MapMovie).ToList();
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
                $"queue?page={page}&pageSize=100&includeMovie=true",
                apiKey);
            var dto = await ArrHttp.SendAsync<QueuePageDto>(http, request, logger, cancellationToken);
            foreach (var record in dto.Records ?? [])
            {
                if (record.MovieId is { } movieId)
                {
                    entries.Add(new ArrQueueEntry(movieId, record.Size, record.Sizeleft, record.Status));
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
        if (id.TmdbId is not { } tmdbId)
        {
            return null;
        }

        var (baseUrl, apiKey) = await RequireConfigAsync(cancellationToken);
        var movies = await GetListAsync<MovieDto>(baseUrl, apiKey, $"movie?tmdbId={tmdbId}", cancellationToken);
        return movies.Select(MapMovie).FirstOrDefault();
    }

    public async Task<ArrItemSnapshot> AddAsync(ArrAddRequest request, CancellationToken cancellationToken = default)
    {
        if (request is not RadarrAddRequest radarr)
        {
            throw new ArgumentException("Expected RadarrAddRequest.", nameof(request));
        }

        var (baseUrl, apiKey) = await RequireConfigAsync(cancellationToken);
        using var lookupRequest = ArrHttp.Create(
            HttpMethod.Get,
            baseUrl,
            $"movie/lookup?term=tmdb:{radarr.TmdbId}",
            apiKey);
        var candidates = await ArrHttp.SendAsync<List<JsonElement>>(http, lookupRequest, logger, cancellationToken);
        if (candidates.Count == 0)
        {
            throw new ArrClientException(424, $"Radarr lookup returned no results for tmdb:{radarr.TmdbId}");
        }

        var payload = JsonNode.Parse(candidates[0].GetRawText())?.AsObject()
                      ?? throw new ArrClientException(424, "Invalid Radarr lookup payload.");
        payload["qualityProfileId"] = radarr.QualityProfileId;
        payload["rootFolderPath"] = radarr.RootFolderPath;
        payload["monitored"] = radarr.Monitored;
        payload["minimumAvailability"] = radarr.MinimumAvailability;
        payload["tags"] = JsonSerializer.SerializeToNode(radarr.Tags, ArrJson.Options);
        payload["addOptions"] = new JsonObject
        {
            ["monitor"] = "movieOnly",
            ["searchForMovie"] = radarr.SearchForMovie,
        };

        using var post = ArrHttp.Create(HttpMethod.Post, baseUrl, "movie", apiKey);
        post.Content = new StringContent(payload.ToJsonString(ArrJson.Options), System.Text.Encoding.UTF8, "application/json");
        var created = await ArrHttp.SendAsync<MovieDto>(http, post, logger, cancellationToken);
        return MapMovie(created);
    }

    public async Task SearchAsync(int arrId, CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = await RequireConfigAsync(cancellationToken);
        using var request = ArrHttp.Create(HttpMethod.Post, baseUrl, "command", apiKey);
        request.Content = JsonContent.Create(new { name = "MoviesSearch", movieIds = new[] { arrId } }, options: ArrJson.Options);
        await ArrHttp.SendAsync(http, request, logger, cancellationToken);
    }

    public async Task DeleteAsync(int arrId, bool deleteFiles, bool addImportExclusion, CancellationToken cancellationToken = default)
    {
        var (baseUrl, apiKey) = await RequireConfigAsync(cancellationToken);
        using var request = ArrHttp.Create(
            HttpMethod.Delete,
            baseUrl,
            $"movie/{arrId}?deleteFiles={deleteFiles.ToString().ToLowerInvariant()}&addImportExclusion={addImportExclusion.ToString().ToLowerInvariant()}",
            apiKey);
        await ArrHttp.SendAsync(http, request, logger, cancellationToken);
    }

    private async Task<(string Url, string ApiKey)> RequireConfigAsync(CancellationToken cancellationToken)
    {
        var config = await ArrHttp.ResolveConfigAsync(
            settings,
            SettingsKeys.RadarrUrl,
            SettingsKeys.RadarrApiKey,
            cancellationToken);
        return config ?? throw new InvalidOperationException("Radarr is not configured.");
    }

    private async Task<List<T>> GetListAsync<T>(string baseUrl, string apiKey, string path, CancellationToken cancellationToken)
    {
        using var request = ArrHttp.Create(HttpMethod.Get, baseUrl, path, apiKey);
        return await ArrHttp.SendAsync<List<T>>(http, request, logger, cancellationToken);
    }

    private async Task<ArrDefaults?> LoadDefaultsAsync(CancellationToken cancellationToken)
    {
        var raw = await settings.GetRawAsync(SettingsKeys.RadarrDefaults, cancellationToken);
        return string.IsNullOrWhiteSpace(raw) ? null : System.Text.Json.JsonSerializer.Deserialize<ArrDefaults>(raw, ArrJson.Options);
    }

    private static ArrItemSnapshot MapMovie(MovieDto m) =>
        new(
            m.Id,
            m.Title ?? $"Movie {m.Id}",
            m.Year,
            m.TmdbId,
            TvdbId: null,
            m.ImdbId,
            m.Monitored,
            m.Path,
            m.SizeOnDisk,
            m.HasFile,
            m.IsAvailable,
            EpisodeFileCount: null,
            EpisodeCount: null,
            TotalEpisodeCount: null,
            Seasons: []);

    private sealed record StatusDto(string? Version, string? AppName);
    private sealed record QualityProfileDto(int Id, string? Name);
    private sealed record RootFolderDto(string? Path, long? FreeSpace);
    private sealed record TagDto(int Id, string? Label);
    private sealed record ArrDefaults(int? QualityProfileId, string? RootFolderPath);
    private sealed record MovieDto(
        int Id,
        string? Title,
        int? Year,
        int? TmdbId,
        string? ImdbId,
        string? Path,
        bool Monitored,
        bool HasFile,
        bool IsAvailable,
        long SizeOnDisk);
    private sealed record QueuePageDto(int Page, int PageSize, int TotalRecords, List<QueueRecordDto>? Records);
    private sealed record QueueRecordDto(
        int? MovieId,
        double Size,
        [property: JsonPropertyName("sizeleft")] double Sizeleft,
        string? Status);
}
