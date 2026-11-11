using Pickarr.Core.Abstractions;

namespace Pickarr.Infrastructure.Tests;

internal sealed class TestSettingsStore : ISettingsStore
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public void SetRaw(string key, string valueJson) => _values[key] = valueJson;

    public Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_values.TryGetValue(key, out var value) ? value : null);

    public Task SetAsync(string key, string valueJson, bool isSecret, CancellationToken cancellationToken = default)
    {
        _values[key] = valueJson;
        return Task.CompletedTask;
    }

    public Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_values.ContainsKey(key));

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        _values.Remove(key);
        return Task.CompletedTask;
    }
}
