namespace Pickarr.Core.Abstractions;

public interface ISettingsStore
{
    Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string valueJson, bool isSecret, CancellationToken cancellationToken = default);
    Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>True when the key is declared by process configuration and cannot be changed from the UI.</summary>
    bool IsEnvironmentDefined(string key) => false;
}
