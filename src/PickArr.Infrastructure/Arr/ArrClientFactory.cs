using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Settings;

namespace Pickarr.Infrastructure.Arr;

public sealed class ArrClientFactory(
    SonarrClient sonarr,
    RadarrClient radarr,
    ISettingsStore settings) : IArrClientFactory
{
    public async Task<IArrClient?> TryCreateAsync(ArrKind kind, CancellationToken cancellationToken = default)
    {
        return kind switch
        {
            ArrKind.Sonarr when await IsConfiguredAsync(SettingsKeys.SonarrUrl, SettingsKeys.SonarrApiKey, cancellationToken)
                => sonarr,
            ArrKind.Radarr when await IsConfiguredAsync(SettingsKeys.RadarrUrl, SettingsKeys.RadarrApiKey, cancellationToken)
                => radarr,
            _ => null,
        };
    }

    public IArrClient Get(ArrKind kind) => kind switch
    {
        ArrKind.Sonarr => sonarr,
        ArrKind.Radarr => radarr,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported Arr kind."),
    };

    private async Task<bool> IsConfiguredAsync(string urlKey, string apiKeyKey, CancellationToken cancellationToken)
    {
        var config = await ArrHttp.ResolveConfigAsync(settings, urlKey, apiKeyKey, cancellationToken);
        return config is not null;
    }
}
