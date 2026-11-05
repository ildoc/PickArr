namespace PickArr.Core.Abstractions;

public interface ISettingsStore
{
    Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string valueJson, bool isSecret, CancellationToken cancellationToken = default);
    Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default);
    Task DeleteAsync(string key, CancellationToken cancellationToken = default);
}
