using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Catalog;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Metadata;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Catalog;

/// <summary>
/// Sync leaving dates from the catalog provider (spec §6.8 / FR-CAT-11).
/// Free plan: preview only — never clears dates merely because they are absent from the preview.
/// One Watchmode call per country; results fan out to every enabled source in that country.
/// Missing titles / availabilities for matching enabled sources are upserted so the Leaving
/// view works even when the catalog is still incomplete (no full baseline yet).
/// </summary>
public sealed class LeavingSync(
    PickarrDbContext db,
    IProviderResolver providerResolver,
    ICreditBudget creditBudget,
    ISyncRunStore syncRunStore,
    IApiUsageSyncRunAccessor syncRunAccessor,
    ISettingsStore settings,
    IClock clock,
    ILogger<LeavingSync> logger) : ILeavingSync
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CatalogSyncResult> RunAsync(
        int catalogSourceId,
        bool isManual = false,
        CancellationToken cancellationToken = default)
    {
        var source = await db.CatalogSources.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == catalogSourceId, cancellationToken)
            ?? throw new InvalidOperationException($"CatalogSource '{catalogSourceId}' was not found.");

        return await RunForCountryAsync(source.CountryCode, isManual, cancellationToken);
    }

    public async Task<CatalogSyncResult> RunForCountryAsync(
        string countryCode,
        bool isManual = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
        {
            throw new ArgumentException("Country code is required.", nameof(countryCode));
        }

        var normalizedCountry = countryCode.Trim().ToUpperInvariant();
        var sources = await db.CatalogSources
            .Include(s => s.Provider)
            .Where(s =>
                s.IsEnabled
                && s.Provider != null
                && s.Provider.IsEnabled
                && s.CountryCode == normalizedCountry)
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        if (sources.Count == 0)
        {
            throw new InvalidOperationException(
                $"No enabled catalog sources found for country '{normalizedCountry}'.");
        }

        var trigger = isManual ? SyncRunTriggers.Manual : SyncRunTriggers.Schedule;
        // Anchor the run on the first source so admin history stays linked to a catalog row.
        var run = await syncRunStore.StartAsync(
            SyncRunKinds.Leaving,
            sources[0].Id,
            trigger,
            cancellationToken: cancellationToken);

        syncRunAccessor.CurrentSyncRunId = run.Id;
        try
        {
            var catalogProvider = providerResolver.Resolve(sources[0].Provider!.ProviderType);
            if (!catalogProvider.Capabilities.SupportsLeavingDates)
            {
                await syncRunStore.SucceedAsync(run.Id, cancellationToken);
                return ToResult((await syncRunStore.GetAsync(run.Id, cancellationToken))!);
            }

            const int estimatedCost = 1;
            if (!await creditBudget.TryReserveAsync(run.Id, estimatedCost, isManual, cancellationToken))
            {
                await syncRunStore.PauseAsync(run.Id, "{}", cancellationToken);
                logger.LogInformation(
                    "Leaving sync paused for country {Country} (budget)",
                    normalizedCountry);
                return ToResult((await syncRunStore.GetAsync(run.Id, cancellationToken))!);
            }

            IReadOnlyList<LeavingEntry> leaving;
            try
            {
                var providerExternalId = sources[0].Provider!.ExternalId
                    ?? throw new InvalidOperationException(
                        $"CatalogSource '{sources[0].Id}' has no provider external id.");
                // One HTTP call per country; provider id is unused by Watchmode for this endpoint.
                leaving = (await catalogProvider.GetLeavingAsync(
                        new CatalogSourceRef(providerExternalId, normalizedCountry),
                        cancellationToken))
                    .Where(e =>
                        string.IsNullOrWhiteSpace(e.CountryCode)
                        || string.Equals(e.CountryCode, normalizedCountry, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            catch (WatchmodeQuotaExceededException ex)
            {
                await syncRunStore.PauseAsync(run.Id, "{}", cancellationToken);
                logger.LogWarning(ex, "Watchmode quota exceeded during leaving sync");
                return ToResult((await syncRunStore.GetAsync(run.Id, cancellationToken))!);
            }

            var now = clock.UtcNow;
            var today = DateOnly.FromDateTime(now);
            var plan = await GetPlanAsync(cancellationToken);
            var isPaid = string.Equals(plan, "paid", StringComparison.OrdinalIgnoreCase);

            var announced = 0;
            var cleared = 0;
            var created = 0;

            foreach (var source in sources)
            {
                var forSource = leaving
                    .Where(e =>
                        string.IsNullOrWhiteSpace(e.ProviderExternalId)
                        || string.Equals(
                            e.ProviderExternalId,
                            source.Provider!.ExternalId,
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();

                var (sourceAnnounced, sourceCleared, sourceCreated) = await ApplyToSourceAsync(
                    source.Id,
                    source.BaselineCompletedAt is not null,
                    forSource,
                    run.Id,
                    now,
                    today,
                    isPaid,
                    cancellationToken);
                announced += sourceAnnounced;
                cleared += sourceCleared;
                created += sourceCreated;
            }

            await db.SaveChangesAsync(cancellationToken);

            var tracked = await syncRunStore.GetAsync(run.Id, cancellationToken);
            if (tracked is not null)
            {
                tracked.PagesFetched = 1;
                tracked.TitlesAdded = created;
                tracked.TitlesUpdated = announced + cleared;
                await syncRunStore.UpdateAsync(tracked, cancellationToken);
            }

            await syncRunStore.SucceedAsync(run.Id, cancellationToken);
            logger.LogInformation(
                "Leaving sync for country {Country} ({SourceCount} sources): announced={Announced}, cleared={Cleared}, created={Created}, paid={Paid}",
                normalizedCountry,
                sources.Count,
                announced,
                cleared,
                created,
                isPaid);
            return ToResult((await syncRunStore.GetAsync(run.Id, cancellationToken))!);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Leaving sync failed for country {Country}", normalizedCountry);
            await syncRunStore.FailAsync(run.Id, ex.Message, cancellationToken);
            return ToResult((await syncRunStore.GetAsync(run.Id, cancellationToken))!);
        }
        finally
        {
            syncRunAccessor.CurrentSyncRunId = null;
        }
    }

    private async Task<(int Announced, int Cleared, int Created)> ApplyToSourceAsync(
        int catalogSourceId,
        bool baselineCompleted,
        IReadOnlyList<LeavingEntry> leaving,
        Guid runId,
        DateTime now,
        DateOnly today,
        bool isPaid,
        CancellationToken cancellationToken)
    {
        var watchmodeIds = leaving
            .Select(e => int.TryParse(e.ExternalId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : (int?)null)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var titles = await db.Titles
            .Where(t => t.WatchmodeId != null && watchmodeIds.Contains(t.WatchmodeId.Value))
            .ToDictionaryAsync(t => t.WatchmodeId!.Value, cancellationToken);

        // Include removed rows so a leaving hit can revive availability on this source.
        var availabilities = await db.Availabilities
            .Where(a => a.CatalogSourceId == catalogSourceId)
            .ToListAsync(cancellationToken);
        var byTitleId = availabilities.ToDictionary(a => a.TitleId);

        var announced = 0;
        var created = 0;
        var seenAvailabilityIds = new HashSet<int>();

        foreach (var entry in leaving)
        {
            if (!int.TryParse(entry.ExternalId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wmId))
            {
                continue;
            }

            // Free preview can include same-day rows; ignore already-elapsed dates.
            if (entry.LeavingDate < today)
            {
                continue;
            }

            if (!titles.TryGetValue(wmId, out var title))
            {
                title = CreateTitleFromLeaving(entry, wmId, now);
                // Need a real Title.Id before attaching Availability / looking up byTitleId.
                await db.SaveChangesAsync(cancellationToken);
                titles[wmId] = title;
                created++;
            }

            if (!byTitleId.TryGetValue(title.Id, out var availability))
            {
                availability = CreateAvailability(title, catalogSourceId, baselineCompleted, now);
                // Persist so AvailabilityEvent can reference a real Id within this run.
                await db.SaveChangesAsync(cancellationToken);
                byTitleId[title.Id] = availability;
                availabilities.Add(availability);
            }
            else if (availability.RemovedAt is not null)
            {
                availability.RemovedAt = null;
                availability.AddedAt = baselineCompleted ? now : availability.AddedAt;
                availability.IsBaseline = !baselineCompleted;
                availability.MissedFullScans = 0;
                availability.LastSeenAt = now;
                db.AvailabilityEvents.Add(new AvailabilityEvent
                {
                    AvailabilityId = availability.Id,
                    Type = AvailabilityEventTypes.Readded,
                    OccurredAt = now,
                    SyncRunId = runId,
                });
            }

            seenAvailabilityIds.Add(availability.Id);

            if (availability.LeavingDate == entry.LeavingDate)
            {
                continue;
            }

            availability.LeavingDate = entry.LeavingDate;
            availability.LastSeenAt = now;
            db.AvailabilityEvents.Add(new AvailabilityEvent
            {
                AvailabilityId = availability.Id,
                Type = AvailabilityEventTypes.LeavingAnnounced,
                OccurredAt = now,
                SyncRunId = runId,
                Details = JsonSerializer.Serialize(
                    new { leavingDate = entry.LeavingDate, season = entry.Season },
                    JsonOptions),
            });
            announced++;
        }

        var cleared = 0;
        foreach (var availability in availabilities.Where(a => a.RemovedAt == null && a.LeavingDate is not null))
        {
            var shouldClear = availability.LeavingDate < today
                || (isPaid && !seenAvailabilityIds.Contains(availability.Id));

            if (!shouldClear)
            {
                continue;
            }

            var previous = availability.LeavingDate;
            availability.LeavingDate = null;
            db.AvailabilityEvents.Add(new AvailabilityEvent
            {
                AvailabilityId = availability.Id,
                Type = AvailabilityEventTypes.LeavingCleared,
                OccurredAt = now,
                SyncRunId = runId,
                Details = JsonSerializer.Serialize(new { previousLeavingDate = previous }, JsonOptions),
            });
            cleared++;
        }

        return (announced, cleared, created);
    }

    private Title CreateTitleFromLeaving(LeavingEntry entry, int watchmodeId, DateTime now)
    {
        var title = new Title
        {
            WatchmodeId = watchmodeId,
            MediaType = CatalogMediaMapping.ToMediaType(entry.Type),
            Category = CatalogMediaMapping.ToCategory(entry.Type),
            OriginalTitle = entry.Title,
            OriginalTitleNormalized = TextNormalizer.NormalizeName(entry.Title),
            MetadataStatus = CatalogMediaMapping.InitialMetadataStatus(tmdbId: null),
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Titles.Add(title);
        return title;
    }

    private Availability CreateAvailability(
        Title title,
        int catalogSourceId,
        bool baselineCompleted,
        DateTime now)
    {
        var availability = new Availability
        {
            TitleId = title.Id,
            CatalogSourceId = catalogSourceId,
            FirstSeenAt = now,
            LastSeenAt = now,
            IsBaseline = !baselineCompleted,
            AddedAt = baselineCompleted ? now : null,
            MissedFullScans = 0,
        };
        db.Availabilities.Add(availability);
        return availability;
    }

    private async Task<string> GetPlanAsync(CancellationToken cancellationToken)
    {
        var raw = await settings.GetRawAsync(SettingsKeys.WatchmodePlan, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "free";
        }

        try
        {
            return JsonSerializer.Deserialize<string>(raw) ?? "free";
        }
        catch (JsonException)
        {
            return raw.Trim('"');
        }
    }

    private static CatalogSyncResult ToResult(SyncRun run) =>
        new(
            run.Id,
            run.Status,
            run.PagesFetched,
            run.CreditsUsed,
            run.TitlesAdded,
            run.TitlesUpdated,
            run.CheckpointJson,
            run.ErrorMessage);
}
