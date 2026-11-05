using System.Text.Json;
using PickArr.Core.Abstractions;
using PickArr.Core.Arr;
using PickArr.Core.Domain;
using PickArr.Core.Settings;

namespace PickArr.Api.Endpoints;

public static class ArrEndpoints
{
    public static IEndpointRouteBuilder MapArrEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/arr");

        group.MapGet("/options", async (
            string? kind,
            IArrClientFactory factory,
            CancellationToken ct) =>
        {
            if (!TryParseKind(kind, out var arrKind))
            {
                return Results.BadRequest(new { code = "INVALID_KIND" });
            }

            var client = await factory.TryCreateAsync(arrKind, ct);
            if (client is null)
            {
                return Results.Ok(new
                {
                    kind = arrKind.ToApi(),
                    configured = false,
                    reachable = false,
                    qualityProfiles = Array.Empty<object>(),
                    rootFolders = Array.Empty<object>(),
                    tags = Array.Empty<object>(),
                    defaults = (object?)null,
                });
            }

            try
            {
                var options = await client.GetOptionsAsync(ct);
                return Results.Ok(new
                {
                    kind = arrKind.ToApi(),
                    configured = true,
                    reachable = true,
                    qualityProfiles = options.QualityProfiles.Select(p => new { id = p.Id, name = p.Name }),
                    rootFolders = options.RootFolders.Select(r => new { path = r.Path, freeSpace = r.FreeSpace }),
                    tags = options.Tags.Select(t => new { id = t.Id, label = t.Label }),
                    defaults = new
                    {
                        qualityProfileId = options.DefaultQualityProfileId,
                        rootFolderPath = options.DefaultRootFolderPath,
                    },
                    monitorOptions = arrKind == ArrKind.Sonarr
                        ? new[]
                        {
                            "all", "future", "missing", "existing", "firstSeason", "lastSeason",
                            "latestSeason", "pilot", "recent", "none",
                        }
                        : null,
                    seriesTypes = arrKind == ArrKind.Sonarr ? new[] { "standard", "daily", "anime" } : null,
                    minimumAvailabilityOptions = arrKind == ArrKind.Radarr
                        ? new[] { "announced", "inCinemas", "released" }
                        : null,
                });
            }
            catch (Exception)
            {
                return Results.Ok(new
                {
                    kind = arrKind.ToApi(),
                    configured = true,
                    reachable = false,
                    qualityProfiles = Array.Empty<object>(),
                    rootFolders = Array.Empty<object>(),
                    tags = Array.Empty<object>(),
                    defaults = (object?)null,
                });
            }
        });

        group.MapPost("/test", async (
            ArrTestRequest body,
            IArrClientFactory factory,
            ISettingsStore settings,
            CancellationToken ct) =>
        {
            if (!TryParseKind(body.Kind, out var arrKind))
            {
                return Results.BadRequest(new { ok = false, error = "Invalid kind" });
            }

            var (urlKey, apiKeyKey) = arrKind == ArrKind.Sonarr
                ? (SettingsKeys.SonarrUrl, SettingsKeys.SonarrApiKey)
                : (SettingsKeys.RadarrUrl, SettingsKeys.RadarrApiKey);

            var url = body.Url?.Trim().TrimEnd('/');
            var apiKey = body.ApiKey?.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                url = await ReadJsonStringAsync(settings, urlKey, ct);
            }

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = await ReadJsonStringAsync(settings, apiKeyKey, ct);
            }

            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(apiKey))
            {
                return Results.Ok(EmptyProbe(ok: false, error: "notConfigured"));
            }

            try
            {
                var probe = await factory.Get(arrKind).ProbeAsync(url, apiKey, ct);
                return Results.Ok(new
                {
                    ok = true,
                    version = probe.Version,
                    appName = probe.AppName,
                    qualityProfiles = probe.QualityProfiles.Select(p => new { id = p.Id, name = p.Name }),
                    rootFolders = probe.RootFolders.Select(r => new { path = r.Path, freeSpace = r.FreeSpace }),
                });
            }
            catch (Exception ex)
            {
                return Results.Ok(EmptyProbe(ok: false, error: ex.Message));
            }
        });

        return endpoints;
    }

    private static bool TryParseKind(string? kind, out ArrKind arrKind)
    {
        arrKind = ArrKind.Sonarr;
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        return Enum.TryParse(kind, ignoreCase: true, out arrKind);
    }

    private static object EmptyProbe(bool ok, string error) => new
    {
        ok,
        error,
        qualityProfiles = Array.Empty<object>(),
        rootFolders = Array.Empty<object>(),
    };

    private static async Task<string?> ReadJsonStringAsync(ISettingsStore settings, string key, CancellationToken cancellationToken)
    {
        var raw = await settings.GetRawAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<string>(raw);
        }
        catch (JsonException)
        {
            return raw.Trim().Trim('"');
        }
    }

    private sealed record ArrTestRequest(string Kind, string? Url, string? ApiKey);
}
