using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Catalog;

public sealed class RecoEventLogService(
    PickarrDbContext db,
    IClock clock,
    ISettingsStore settings) : IRecoEventLogService
{
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
    {
        var raw = await settings.GetRawAsync(SettingsKeys.RecoEventsEnabled, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return bool.TryParse(raw.Trim().Trim('"'), out var enabled) && enabled;
    }

    public async Task LogAsync(
        int titleId,
        string eventKind,
        int? version = null,
        int? rank = null,
        string? scope = null,
        CancellationToken cancellationToken = default)
    {
        await LogManyAsync(
            [new RecoEventLogEntry(titleId, eventKind, version, rank, scope)],
            cancellationToken);
    }

    public async Task LogManyAsync(
        IReadOnlyList<RecoEventLogEntry> entries,
        CancellationToken cancellationToken = default)
    {
        if (entries.Count == 0 || !await IsEnabledAsync(cancellationToken))
        {
            return;
        }

        var now = clock.UtcNow;
        var rows = new List<RecoEventLog>(entries.Count);
        foreach (var entry in entries)
        {
            if (!RecoEventKinds.IsValid(entry.Event))
            {
                continue;
            }

            rows.Add(new RecoEventLog
            {
                At = now,
                TitleId = entry.TitleId,
                Event = RecoEventKinds.Normalize(entry.Event),
                Version = entry.Version,
                Rank = entry.Rank,
                Scope = entry.Scope,
            });
        }

        if (rows.Count == 0)
        {
            return;
        }

        db.RecoEventLogs.AddRange(rows);
        await db.SaveChangesAsync(cancellationToken);
    }
}
