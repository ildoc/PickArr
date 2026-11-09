using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain.Entities;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Settings;

public sealed class ProtectedSettingsStore(
    PickarrDbContext db,
    IDataProtectionProvider dataProtectionProvider,
    IClock clock) : ISettingsStore
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Pickarr.Settings.v1");

    public async Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (setting is null)
        {
            return null;
        }

        if (!setting.IsSecret)
        {
            return setting.ValueJson;
        }

        try
        {
            var plain = _protector.Unprotect(setting.ValueJson);
            return JsonSerializer.Serialize(plain);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetAsync(string key, string valueJson, bool isSecret, CancellationToken cancellationToken = default)
    {
        var storedValue = valueJson;
        if (isSecret)
        {
            var plain = JsonSerializer.Deserialize<string>(valueJson)
                ?? throw new InvalidOperationException($"Secret setting '{key}' must be a JSON string.");
            storedValue = _protector.Protect(plain);
        }

        var existing = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (existing is null)
        {
            db.Settings.Add(new AppSetting
            {
                Key = key,
                ValueJson = storedValue,
                IsSecret = isSecret,
                UpdatedAt = clock.UtcNow,
            });
        }
        else
        {
            existing.ValueJson = storedValue;
            existing.IsSecret = isSecret;
            existing.UpdatedAt = clock.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default)
    {
        var setting = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        return setting is not null && !string.IsNullOrWhiteSpace(setting.ValueJson);
    }

    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var existing = await db.Settings.FirstOrDefaultAsync(s => s.Key == key, cancellationToken);
        if (existing is null)
        {
            return;
        }

        db.Settings.Remove(existing);
        await db.SaveChangesAsync(cancellationToken);
    }
}
