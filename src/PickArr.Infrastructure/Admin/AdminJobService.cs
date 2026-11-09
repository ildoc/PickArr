using Hangfire;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Admin;
using Pickarr.Core.Domain;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Jobs;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Admin;

public sealed class AdminJobService(
    PickarrDbContext db,
    IBackgroundJobClient backgroundJobs,
    ISettingsStore settings,
    CatalogBootstrap bootstrap) : IAdminJobService
{
    public async Task<IReadOnlyList<AdminJobDto>> ListJobsAsync(CancellationToken cancellationToken = default)
    {
        var sources = await db.CatalogSources.AsNoTracking()
            .Where(s => s.IsEnabled)
            .OrderBy(s => s.CountryCode)
            .ToListAsync(cancellationToken);

        var recurring = LoadRecurring();
        var activity = HangfireJobActivity.Read();
        var watchmodeReady = await settings.IsSetAsync(SettingsKeys.WatchmodeApiKey, cancellationToken);
        var jobs = new List<AdminJobDto>();

        foreach (var source in sources)
        {
            jobs.Add(await BuildJobAsync(
                ScheduleReconciler.IncrementalJobId(source.Id),
                JobText.IncrementalName(source.CountryCode),
                JobText.IncrementalDescription(source.CountryCode),
                SyncRunKinds.CatalogIncremental,
                source.Id,
                source.CountryCode,
                source.IncrementalCron,
                includeRuns: true,
                canTrigger: true,
                recurring,
                activity,
                cancellationToken));

            jobs.Add(await BuildJobAsync(
                ScheduleReconciler.FullJobId(source.Id),
                JobText.FullName(source.CountryCode),
                JobText.FullDescription(source.CountryCode),
                SyncRunKinds.CatalogFull,
                source.Id,
                source.CountryCode,
                source.FullSyncCron,
                includeRuns: true,
                canTrigger: true,
                recurring,
                activity,
                cancellationToken));
        }

        foreach (var countryGroup in sources
                     .GroupBy(s => s.CountryCode.Trim().ToUpperInvariant(), StringComparer.Ordinal)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var anchor = countryGroup.OrderBy(s => s.Id).First();
            jobs.Add(await BuildJobAsync(
                LeavingSyncJob.JobId(countryGroup.Key),
                JobText.LeavingName(countryGroup.Key),
                JobText.LeavingDescription(countryGroup.Key),
                SyncRunKinds.Leaving,
                anchor.Id,
                countryGroup.Key,
                LeavingSyncJob.Cron,
                includeRuns: true,
                canTrigger: true,
                recurring,
                activity,
                cancellationToken));
        }

        jobs.Add(await BuildJobAsync(
            CatalogBootstrapJob.JobKey,
            JobText.BootstrapName,
            JobText.BootstrapDescription,
            kind: "CatalogBootstrap",
            sourceId: null,
            countryCode: null,
            cron: null,
            includeRuns: false,
            canTrigger: watchmodeReady,
            recurring,
            activity,
            cancellationToken));

        jobs.Add(await BuildJobAsync(
            ScheduleReconciler.ResumeJobId,
            JobText.ResumeName,
            JobText.ResumeDescription,
            kind: "CatalogResume",
            sourceId: null,
            countryCode: null,
            cron: null,
            includeRuns: false,
            canTrigger: true,
            recurring,
            activity,
            cancellationToken));

        jobs.Add(await BuildJobAsync(
            EnrichmentJob.RecurringJobId,
            JobText.EnrichmentName,
            JobText.EnrichmentDescription,
            SyncRunKinds.Enrichment,
            sourceId: null,
            countryCode: null,
            cron: null,
            includeRuns: true,
            canTrigger: true,
            recurring,
            activity,
            cancellationToken));

        jobs.Add(await BuildJobAsync(
            MetadataRefreshJob.RecurringJobId,
            JobText.RefreshName,
            JobText.RefreshDescription,
            SyncRunKinds.MetadataRefresh,
            sourceId: null,
            countryCode: null,
            cron: null,
            includeRuns: true,
            canTrigger: true,
            recurring,
            activity,
            cancellationToken));

        jobs.Add(await BuildJobAsync(
            ArrSyncJob.SonarrJobId,
            JobText.SonarrName,
            JobText.SonarrDescription,
            SyncRunKinds.ArrSync,
            sourceId: null,
            countryCode: null,
            cron: null,
            includeRuns: true,
            canTrigger: true,
            recurring,
            activity,
            cancellationToken));

        jobs.Add(await BuildJobAsync(
            ArrSyncJob.RadarrJobId,
            JobText.RadarrName,
            JobText.RadarrDescription,
            SyncRunKinds.ArrSync,
            sourceId: null,
            countryCode: null,
            cron: null,
            includeRuns: false,
            canTrigger: true,
            recurring,
            activity,
            cancellationToken));

        return jobs;
    }

    public async Task<TriggerJobResult> TriggerAsync(
        string jobKey,
        TriggerJobRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            throw new ArgumentException("jobKey is required.", nameof(jobKey));
        }

        jobKey = jobKey.Trim();

        if (string.Equals(jobKey, "catalog", StringComparison.OrdinalIgnoreCase))
        {
            if (request.SourceId is not { } sourceId)
            {
                throw new ArgumentException("sourceId is required for catalog triggers.");
            }

            var mode = (request.Mode ?? "incremental").Trim().ToLowerInvariant();
            return mode switch
            {
                "incremental" => await TriggerCatalogIncrementalAsync(sourceId, cancellationToken),
                "full" => await TriggerCatalogFullAsync(sourceId, cancellationToken),
                _ => throw new ArgumentException($"Unsupported catalog mode '{request.Mode}'."),
            };
        }

        if (jobKey.StartsWith("catalog-incr-", StringComparison.OrdinalIgnoreCase))
        {
            var sourceId = ParseTrailingId(jobKey, "catalog-incr-");
            return await TriggerCatalogIncrementalAsync(sourceId, cancellationToken);
        }

        if (jobKey.StartsWith("catalog-full-", StringComparison.OrdinalIgnoreCase))
        {
            var sourceId = ParseTrailingId(jobKey, "catalog-full-");
            return await TriggerCatalogFullAsync(sourceId, cancellationToken);
        }

        if (jobKey.StartsWith("catalog-leaving-", StringComparison.OrdinalIgnoreCase))
        {
            var country = ParseTrailingCountry(jobKey, "catalog-leaving-");
            return await TriggerLeavingAsync(country, cancellationToken);
        }

        if (string.Equals(jobKey, EnrichmentJob.RecurringJobId, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureNotRunningAsync(SyncRunKinds.Enrichment, sourceId: null, cancellationToken);
            var hf = backgroundJobs.Enqueue<EnrichmentJob>(j => j.ExecuteAsync(true, CancellationToken.None));
            var runId = await WaitForRunAsync(SyncRunKinds.Enrichment, sourceId: null, cancellationToken);
            return new TriggerJobResult(runId, hf);
        }

        if (string.Equals(jobKey, MetadataRefreshJob.RecurringJobId, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureNotRunningAsync(SyncRunKinds.MetadataRefresh, sourceId: null, cancellationToken);
            var hf = backgroundJobs.Enqueue<MetadataRefreshJob>(j => j.ExecuteAsync(true, CancellationToken.None));
            var runId = await WaitForRunAsync(SyncRunKinds.MetadataRefresh, sourceId: null, cancellationToken);
            return new TriggerJobResult(runId, hf);
        }

        if (string.Equals(jobKey, ArrSyncJob.SonarrJobId, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureNotRunningAsync(SyncRunKinds.ArrSync, sourceId: null, cancellationToken);
            var hf = backgroundJobs.Enqueue<ArrSyncJob>(j => j.ExecuteSonarrAsync(CancellationToken.None));
            var runId = await WaitForRunAsync(SyncRunKinds.ArrSync, sourceId: null, cancellationToken);
            return new TriggerJobResult(runId, hf);
        }

        if (string.Equals(jobKey, ArrSyncJob.RadarrJobId, StringComparison.OrdinalIgnoreCase))
        {
            await EnsureNotRunningAsync(SyncRunKinds.ArrSync, sourceId: null, cancellationToken);
            var hf = backgroundJobs.Enqueue<ArrSyncJob>(j => j.ExecuteRadarrAsync(CancellationToken.None));
            var runId = await WaitForRunAsync(SyncRunKinds.ArrSync, sourceId: null, cancellationToken);
            return new TriggerJobResult(runId, hf);
        }

        if (string.Equals(jobKey, ScheduleReconciler.ResumeJobId, StringComparison.OrdinalIgnoreCase))
        {
            var hf = backgroundJobs.Enqueue<CatalogResumeJob>(j => j.ExecuteAsync(CancellationToken.None));
            return new TriggerJobResult(null, hf);
        }

        if (string.Equals(jobKey, CatalogBootstrapJob.JobKey, StringComparison.OrdinalIgnoreCase))
        {
            var hf = await bootstrap.StartAsync(force: true, cancellationToken)
                ?? throw new ArgumentException("Initial catalog import was not queued.");
            return new TriggerJobResult(null, hf);
        }

        throw new ArgumentException($"Unknown jobKey '{jobKey}'.");
    }

    public async Task<PagedAdminRunsDto> ListRunsAsync(
        string? kind,
        string? status,
        int page,
        int pageSize,
        string? jobKey = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.SyncRuns.AsNoTracking().AsQueryable();
        if (TryResolveJobFilter(jobKey, out var jobKind, out var sourceId))
        {
            query = query.Where(r => r.Kind == jobKind);
            query = sourceId is null
                ? query.Where(r => r.CatalogSourceId == null)
                : query.Where(r => r.CatalogSourceId == sourceId);
        }
        else if (!string.IsNullOrWhiteSpace(kind))
        {
            query = query.Where(r => r.Kind == kind);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(r => r.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var sourceIds = rows.Where(r => r.CatalogSourceId is not null).Select(r => r.CatalogSourceId!.Value).Distinct().ToList();
        var countries = await db.CatalogSources.AsNoTracking()
            .Where(s => sourceIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.CountryCode, cancellationToken);

        var items = rows.Select(r => ToSummary(r, r.CatalogSourceId is { } sid ? countries.GetValueOrDefault(sid) : null)).ToList();
        return new PagedAdminRunsDto(page, pageSize, total, items);
    }

    public async Task<AdminRunSummaryDto?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        var run = await db.SyncRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (run is null)
        {
            return null;
        }

        string? country = null;
        if (run.CatalogSourceId is { } sid)
        {
            country = await db.CatalogSources.AsNoTracking()
                .Where(s => s.Id == sid)
                .Select(s => s.CountryCode)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return ToSummary(run, country);
    }

    private async Task<TriggerJobResult> TriggerCatalogIncrementalAsync(int sourceId, CancellationToken cancellationToken)
    {
        await EnsureSourceExistsAsync(sourceId, cancellationToken);
        await EnsureNotRunningAsync(SyncRunKinds.CatalogIncremental, sourceId, cancellationToken);
        var hf = backgroundJobs.Enqueue<CatalogIncrementalSyncJob>(j => j.ExecuteAsync(sourceId, true, null, CancellationToken.None));
        var runId = await WaitForRunAsync(SyncRunKinds.CatalogIncremental, sourceId, cancellationToken);
        return new TriggerJobResult(runId, hf);
    }

    private async Task<TriggerJobResult> TriggerCatalogFullAsync(int sourceId, CancellationToken cancellationToken)
    {
        await EnsureSourceExistsAsync(sourceId, cancellationToken);
        await EnsureNotRunningAsync(SyncRunKinds.CatalogFull, sourceId, cancellationToken);
        var hf = backgroundJobs.Enqueue<CatalogFullSyncJob>(j => j.ExecuteAsync(sourceId, true, null, CancellationToken.None));
        var runId = await WaitForRunAsync(SyncRunKinds.CatalogFull, sourceId, cancellationToken);
        return new TriggerJobResult(runId, hf);
    }

    private async Task<TriggerJobResult> TriggerLeavingAsync(string countryCode, CancellationToken cancellationToken)
    {
        var normalized = countryCode.Trim().ToUpperInvariant();
        var anchorId = await db.CatalogSources.AsNoTracking()
            .Where(s => s.IsEnabled && s.CountryCode == normalized)
            .OrderBy(s => s.Id)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ArgumentException($"No enabled catalog source for country {normalized}.");

        await EnsureNotRunningAsync(SyncRunKinds.Leaving, anchorId, cancellationToken);
        var hf = backgroundJobs.Enqueue<LeavingSyncJob>(j => j.ExecuteAsync(normalized, true, CancellationToken.None));
        var runId = await WaitForRunAsync(SyncRunKinds.Leaving, anchorId, cancellationToken);
        return new TriggerJobResult(runId, hf);
    }

    private async Task EnsureSourceExistsAsync(int sourceId, CancellationToken cancellationToken)
    {
        if (!await db.CatalogSources.AnyAsync(s => s.Id == sourceId, cancellationToken))
        {
            throw new ArgumentException($"Catalog source {sourceId} was not found.");
        }
    }

    private async Task EnsureNotRunningAsync(string kind, int? sourceId, CancellationToken cancellationToken)
    {
        var query = db.SyncRuns.AsNoTracking()
            .Where(r => r.Status == SyncRunStatuses.Running && r.Kind == kind);
        if (sourceId is not null)
        {
            query = query.Where(r => r.CatalogSourceId == sourceId);
        }

        if (await query.AnyAsync(cancellationToken))
        {
            throw new SyncAlreadyRunningException(kind, sourceId);
        }
    }

    private async Task<Guid?> WaitForRunAsync(string kind, int? sourceId, CancellationToken cancellationToken)
    {
        for (var i = 0; i < 25; i++)
        {
            var query = db.SyncRuns.AsNoTracking()
                .Where(r => r.Status == SyncRunStatuses.Running && r.Kind == kind);
            if (sourceId is not null)
            {
                query = query.Where(r => r.CatalogSourceId == sourceId);
            }

            var id = await query.OrderByDescending(r => r.StartedAt).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(cancellationToken);
            if (id is not null)
            {
                return id;
            }

            await Task.Delay(100, cancellationToken);
        }

        return null;
    }

    private async Task<AdminJobDto> BuildJobAsync(
        string jobKey,
        string displayName,
        string description,
        string kind,
        int? sourceId,
        string? countryCode,
        string? cron,
        bool includeRuns,
        bool canTrigger,
        IReadOnlyDictionary<string, RecurringJobDto> recurring,
        HangfireJobActivity activity,
        CancellationToken cancellationToken)
    {
        recurring.TryGetValue(jobKey, out var hf);
        var last = includeRuns ? await GetLatestRunAsync(kind, sourceId, cancellationToken) : null;
        var schedule = hf?.Cron ?? cron;
        var (state, error) = JobExecutionStateResolver.Resolve(
            last?.Status,
            last?.ErrorMessage,
            activity.Running.Contains(jobKey),
            activity.Queued.Contains(jobKey),
            activity.FailedErrors.TryGetValue(jobKey, out var hangfireError) ? hangfireError : null,
            activity.Succeeded.Contains(jobKey),
            hasCron: !string.IsNullOrWhiteSpace(schedule));

        return new AdminJobDto(
            jobKey,
            displayName,
            kind,
            sourceId,
            countryCode,
            schedule,
            last?.StartedAt ?? hf?.LastExecution,
            hf?.NextExecution,
            last?.Status ?? hf?.LastJobState,
            last?.Id,
            canTrigger,
            state,
            error,
            description);
    }

    private async Task<Core.Domain.Entities.SyncRun?> GetLatestRunAsync(
        string kind,
        int? sourceId,
        CancellationToken cancellationToken)
    {
        var query = db.SyncRuns.AsNoTracking().Where(r => r.Kind == kind);
        if (sourceId is not null)
        {
            query = query.Where(r => r.CatalogSourceId == sourceId);
        }

        return await query.OrderByDescending(r => r.StartedAt).FirstOrDefaultAsync(cancellationToken);
    }

    private static IReadOnlyDictionary<string, RecurringJobDto> LoadRecurring()
    {
        try
        {
            using var connection = JobStorage.Current.GetConnection();
            return connection.GetRecurringJobs().ToDictionary(j => j.Id, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, RecurringJobDto>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private bool TryResolveJobFilter(string? jobKey, out string kind, out int? sourceId)
    {
        kind = "";
        sourceId = null;
        if (string.IsNullOrWhiteSpace(jobKey))
        {
            return false;
        }

        jobKey = jobKey.Trim();
        if (jobKey.StartsWith("catalog-incr-", StringComparison.OrdinalIgnoreCase))
        {
            kind = SyncRunKinds.CatalogIncremental;
            sourceId = ParseTrailingId(jobKey, "catalog-incr-");
            return true;
        }

        if (jobKey.StartsWith("catalog-full-", StringComparison.OrdinalIgnoreCase))
        {
            kind = SyncRunKinds.CatalogFull;
            sourceId = ParseTrailingId(jobKey, "catalog-full-");
            return true;
        }

        if (jobKey.StartsWith("catalog-leaving-", StringComparison.OrdinalIgnoreCase))
        {
            kind = SyncRunKinds.Leaving;
            var country = ParseTrailingCountry(jobKey, "catalog-leaving-");
            // Runs are anchored on the lowest enabled source id for that country.
            sourceId = db.CatalogSources.AsNoTracking()
                .Where(s => s.IsEnabled && s.CountryCode == country)
                .OrderBy(s => s.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefault();
            return true;
        }

        if (string.Equals(jobKey, EnrichmentJob.RecurringJobId, StringComparison.OrdinalIgnoreCase))
        {
            kind = SyncRunKinds.Enrichment;
            return true;
        }

        if (string.Equals(jobKey, MetadataRefreshJob.RecurringJobId, StringComparison.OrdinalIgnoreCase))
        {
            kind = SyncRunKinds.MetadataRefresh;
            return true;
        }

        if (string.Equals(jobKey, ArrSyncJob.SonarrJobId, StringComparison.OrdinalIgnoreCase)
            || string.Equals(jobKey, ArrSyncJob.RadarrJobId, StringComparison.OrdinalIgnoreCase))
        {
            kind = SyncRunKinds.ArrSync;
            return true;
        }

        return false;
    }

    private static int ParseTrailingId(string jobKey, string prefix)
    {
        var raw = jobKey[prefix.Length..];
        if (!int.TryParse(raw, out var id))
        {
            throw new ArgumentException($"Invalid jobKey '{jobKey}'.");
        }

        return id;
    }

    private static string ParseTrailingCountry(string jobKey, string prefix)
    {
        var raw = jobKey[prefix.Length..].Trim();
        if (string.IsNullOrWhiteSpace(raw) || raw.All(char.IsDigit))
        {
            throw new ArgumentException($"Invalid leaving jobKey '{jobKey}' (expected country code).");
        }

        return raw.ToUpperInvariant();
    }

    private static AdminRunSummaryDto ToSummary(Core.Domain.Entities.SyncRun run, string? countryCode) =>
        new(
            run.Id,
            run.Kind,
            run.CatalogSourceId,
            countryCode,
            run.TriggeredBy,
            run.Status,
            run.StartedAt,
            run.FinishedAt,
            run.CreditsUsed,
            run.PagesFetched,
            run.TitlesAdded,
            run.TitlesUpdated,
            run.TitlesRemoved,
            run.ErrorMessage);
}
