using PickArr.Core.Abstractions;
using PickArr.Core.Domain;

namespace PickArr.Infrastructure.Jobs;

internal static class MetadataJobRun
{
    public static async Task ExecuteAsync(
        ISyncRunStore syncRuns,
        string kind,
        bool isManual,
        Func<CancellationToken, Task<EnrichmentBatchResult>> nextBatch,
        int maxBatches,
        CancellationToken cancellationToken)
    {
        var trigger = isManual ? SyncRunTriggers.Manual : SyncRunTriggers.Schedule;
        var run = await syncRuns.StartAsync(kind, catalogSourceId: null, trigger, cancellationToken: cancellationToken);
        try
        {
            var processed = 0;
            var ready = 0;
            var failed = 0;
            string? lastError = null;
            for (var i = 0; i < maxBatches; i++)
            {
                var result = await nextBatch(cancellationToken);
                processed += result.Processed;
                ready += result.Ready;
                failed += result.Failed;
                if (!string.IsNullOrWhiteSpace(result.LastError))
                {
                    lastError = result.LastError;
                }

                if (result.Processed == 0 || (result.Ready == 0 && result.Failed > 0))
                {
                    break;
                }
            }

            var tracked = await syncRuns.GetAsync(run.Id, cancellationToken);
            if (tracked is not null)
            {
                tracked.PagesFetched = processed;
                tracked.TitlesUpdated = ready;
                await syncRuns.UpdateAsync(tracked, cancellationToken);
            }

            if (processed > 0 && ready == 0 && failed > 0)
            {
                await syncRuns.FailAsync(
                    run.Id,
                    lastError ?? "TMDB metadata request failed. Check the token in Settings.",
                    cancellationToken);
                return;
            }

            if (failed > 0 && !string.IsNullOrWhiteSpace(lastError))
            {
                var current = await syncRuns.GetAsync(run.Id, cancellationToken);
                if (current is not null)
                {
                    current.ErrorMessage = lastError;
                    await syncRuns.UpdateAsync(current, cancellationToken);
                }
            }

            await syncRuns.SucceedAsync(run.Id, cancellationToken);
        }
        catch (Exception ex)
        {
            await syncRuns.FailAsync(run.Id, ex.Message, cancellationToken);
            throw;
        }
    }
}
