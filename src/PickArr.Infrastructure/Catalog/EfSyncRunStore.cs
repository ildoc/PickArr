using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Catalog;

public sealed class EfSyncRunStore(
    PickarrDbContext db,
    IClock clock,
    ISettingsStore settings) : ISyncRunStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<SyncRun?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        db.SyncRuns.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<SyncRun> AddAsync(SyncRun run, CancellationToken cancellationToken = default)
    {
        db.SyncRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken);
        return run;
    }

    public async Task UpdateAsync(SyncRun run, CancellationToken cancellationToken = default)
    {
        db.SyncRuns.Update(run);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<SyncRun> StartAsync(
        string kind,
        int? catalogSourceId,
        string triggeredBy,
        string? checkpointJson = null,
        CancellationToken cancellationToken = default)
    {
        var run = new SyncRun
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            CatalogSourceId = catalogSourceId,
            TriggeredBy = triggeredBy,
            StartedAt = clock.UtcNow,
            Status = SyncRunStatuses.Running,
            CheckpointJson = checkpointJson,
        };
        return await AddAsync(run, cancellationToken);
    }

    public async Task PauseAsync(Guid id, string checkpointJson, CancellationToken cancellationToken = default)
    {
        var run = await RequireAsync(id, cancellationToken);
        run.Status = SyncRunStatuses.Paused;
        run.CheckpointJson = checkpointJson;
        run.FinishedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await PruneHistoryForAsync(run.Kind, run.CatalogSourceId, cancellationToken);
    }

    public async Task SucceedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var run = await RequireAsync(id, cancellationToken);
        run.Status = SyncRunStatuses.Succeeded;
        run.FinishedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await PruneHistoryForAsync(run.Kind, run.CatalogSourceId, cancellationToken);
    }

    public async Task FailAsync(Guid id, string errorMessage, CancellationToken cancellationToken = default)
    {
        // A failed insert inside a rolled-back transaction stays Added. Saving it again can
        // reuse a rowid that is still tracked and throw an identity conflict.
        foreach (var entry in db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
        {
            entry.State = EntityState.Detached;
        }

        var run = await RequireAsync(id, cancellationToken);
        run.Status = SyncRunStatuses.Failed;
        run.ErrorMessage = Truncate(errorMessage);
        run.FinishedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await PruneHistoryForAsync(run.Kind, run.CatalogSourceId, cancellationToken);
    }

    public async Task MarkNeedsReviewAsync(
        Guid id,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        var run = await RequireAsync(id, cancellationToken);
        run.Status = SyncRunStatuses.NeedsReview;
        run.ErrorMessage = Truncate(errorMessage);
        run.FinishedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        await PruneHistoryForAsync(run.Kind, run.CatalogSourceId, cancellationToken);
    }

    public async Task PruneHistoryAsync(CancellationToken cancellationToken = default)
    {
        var groups = await db.SyncRuns
            .AsNoTracking()
            .Where(r => r.Status != SyncRunStatuses.Running)
            .Select(r => new { r.Kind, r.CatalogSourceId })
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var group in groups)
        {
            await PruneHistoryForAsync(group.Kind, group.CatalogSourceId, cancellationToken);
        }
    }

    private async Task PruneHistoryForAsync(
        string kind,
        int? catalogSourceId,
        CancellationToken cancellationToken)
    {
        var keep = await ReadHistoryLimitAsync(cancellationToken);
        var finished = await db.SyncRuns
            .Where(r =>
                r.Kind == kind
                && r.CatalogSourceId == catalogSourceId
                && r.Status != SyncRunStatuses.Running)
            .OrderByDescending(r => r.StartedAt)
            .ThenByDescending(r => r.Id)
            .Skip(keep)
            .ToListAsync(cancellationToken);

        if (finished.Count == 0)
        {
            return;
        }

        var ids = finished.Select(r => r.Id).ToList();
        var usages = await db.ApiUsages
            .Where(u => u.SyncRunId != null && ids.Contains(u.SyncRunId.Value))
            .ToListAsync(cancellationToken);
        db.ApiUsages.RemoveRange(usages);
        db.SyncRuns.RemoveRange(finished);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> ReadHistoryLimitAsync(CancellationToken cancellationToken)
    {
        var raw = await settings.GetRawAsync(SettingsKeys.JobsRunHistoryLimit, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return SettingsService.DefaultRunHistoryLimit;
        }

        try
        {
            var value = JsonSerializer.Deserialize<int>(raw, JsonOptions);
            return SettingsService.ClampRunHistoryLimit(value);
        }
        catch (JsonException)
        {
            return SettingsService.DefaultRunHistoryLimit;
        }
    }

    private async Task<SyncRun> RequireAsync(Guid id, CancellationToken cancellationToken)
    {
        return await GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"SyncRun '{id}' was not found.");
    }

    private static string? Truncate(string? value) =>
        value is null ? null : value.Length <= 2000 ? value : value[..2000];
}
