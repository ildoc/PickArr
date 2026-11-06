using PickArr.Core.Abstractions;

namespace PickArr.Infrastructure.Settings;

/// <summary>
/// Applies settings declared in configuration before the encrypted database.
/// Writes to a declared key are ignored so the declaration stays the effective value.
/// </summary>
internal sealed class EnvironmentSettingsStore(ISettingsStore inner, EnvironmentSettingSource source) : ISettingsStore
{
    public Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default)
    {
        if (source.TryGet(key, out var json))
        {
            return Task.FromResult<string?>(json);
        }

        return inner.GetRawAsync(key, cancellationToken);
    }

    public Task SetAsync(string key, string valueJson, bool isSecret, CancellationToken cancellationToken = default)
    {
        if (source.TryGet(key, out _))
        {
            return Task.CompletedTask;
        }

        return inner.SetAsync(key, valueJson, isSecret, cancellationToken);
    }

    public Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default)
    {
        if (source.TryGet(key, out _))
        {
            return Task.FromResult(true);
        }

        return inner.IsSetAsync(key, cancellationToken);
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (source.TryGet(key, out _))
        {
            return Task.CompletedTask;
        }

        return inner.DeleteAsync(key, cancellationToken);
    }
}
