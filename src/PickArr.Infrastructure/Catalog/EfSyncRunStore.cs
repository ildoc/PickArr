using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Infrastructure.Persistence;

namespace PickArr.Infrastructure.Catalog;

public sealed class EfSyncRunStore(PickArrDbContext db, IClock clock) : ISyncRunStore
{
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
    }

    public async Task SucceedAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var run = await RequireAsync(id, cancellationToken);
        run.Status = SyncRunStatuses.Succeeded;
        run.FinishedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
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
    }

    private async Task<SyncRun> RequireAsync(Guid id, CancellationToken cancellationToken)
    {
        return await GetAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"SyncRun '{id}' was not found.");
    }

    private static string? Truncate(string? value) =>
        value is null ? null : value.Length <= 2000 ? value : value[..2000];
}
