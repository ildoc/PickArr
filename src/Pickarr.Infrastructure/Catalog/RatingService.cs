using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Metadata;
using Pickarr.Core.Recommendations;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Catalog;

public sealed class RatingService(
    PickarrDbContext db,
    IClock clock,
    ISettingsStore settings,
    IRecoRebuildScheduler rebuildScheduler,
    IRecoEventLogService eventLog) : IRatingService
{
    public async Task SetAsync(
        int profileId,
        int titleId,
        string value,
        string origin,
        CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);

        if (!RatingValues.IsValid(value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Rating must be Love, Like, Dislike, or NotInterested.");
        }

        var normalized = RatingValues.Normalize(value);
        var exists = await db.Titles.AnyAsync(t => t.Id == titleId, cancellationToken);
        if (!exists)
        {
            throw new KeyNotFoundException($"Title {titleId} not found.");
        }

        var now = clock.UtcNow;
        var rating = await db.Ratings.FindAsync([profileId, titleId], cancellationToken);
        if (rating is not null && string.Equals(rating.Value, normalized, StringComparison.Ordinal))
        {
            return;
        }

        if (rating is null)
        {
            db.Ratings.Add(new Rating
            {
                ProfileId = profileId,
                TitleId = titleId,
                Value = normalized,
                RatedAt = now,
            });
        }
        else
        {
            rating.Value = normalized;
            rating.RatedAt = now;
        }

        db.RatingEvents.Add(new RatingEvent
        {
            ProfileId = profileId,
            TitleId = titleId,
            Value = normalized,
            At = now,
            Origin = origin,
        });
        await db.SaveChangesAsync(cancellationToken);
        await eventLog.LogAsync(
            profileId,
            titleId,
            EventKindFor(normalized),
            cancellationToken: cancellationToken);
        await ScheduleTasteRebuildAsync(profileId, cancellationToken);
    }

    private static string EventKindFor(string normalized) =>
        normalized switch
        {
            RatingValues.Dislike => RecoEventKinds.Disliked,
            RatingValues.NotInterested => RecoEventKinds.NotInterested,
            RatingValues.Love => RecoEventKinds.Loved,
            _ => RecoEventKinds.Liked,
        };

    public async Task ClearAsync(int profileId, int titleId, string origin, CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);

        var rating = await db.Ratings.FindAsync([profileId, titleId], cancellationToken);
        if (rating is null)
        {
            return;
        }

        db.Ratings.Remove(rating);
        db.RatingEvents.Add(new RatingEvent
        {
            ProfileId = profileId,
            TitleId = titleId,
            Value = null,
            At = clock.UtcNow,
            Origin = origin,
        });
        await db.SaveChangesAsync(cancellationToken);
        await ScheduleTasteRebuildAsync(profileId, cancellationToken);
    }

    public async Task<IReadOnlyList<RatingExportItemDto>> ExportAsync(
        int profileId,
        CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);

        var rows = await db.Ratings.AsNoTracking()
            .Include(r => r.Title)!.ThenInclude(t => t!.Localizations)
            .Where(r => r.ProfileId == profileId && r.Title != null && r.Title.TmdbId != null && r.Title.TmdbType != null)
            .OrderByDescending(r => r.RatedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(r =>
        {
            var title = r.Title!;
            var name = title.Localizations
                            .OrderByDescending(l => l.Language == LanguageFallback.English)
                            .Select(l => l.Name)
                            .FirstOrDefault()
                        ?? title.OriginalTitle
                        ?? $"Title {title.Id}";
            return new RatingExportItemDto(
                title.TmdbType!,
                title.TmdbId!.Value,
                name,
                RatingValues.ToApi(r.Value),
                r.RatedAt);
        }).ToList();
    }

    public async Task<RatingImportResultDto> ImportAsync(
        int profileId,
        RatingImportRequest request,
        CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);

        var mode = (request.Mode ?? "merge").Trim().ToLowerInvariant();
        if (mode is not ("merge" or "replace"))
        {
            throw new ArgumentException("mode must be merge or replace.", nameof(request));
        }

        var imported = 0;
        var unknown = 0;
        var ignored = 0;
        var now = clock.UtcNow;

        if (mode == "replace")
        {
            var existing = await db.Ratings.Where(r => r.ProfileId == profileId).ToListAsync(cancellationToken);
            db.Ratings.RemoveRange(existing);
            foreach (var rating in existing)
            {
                db.RatingEvents.Add(new RatingEvent
                {
                    ProfileId = profileId,
                    TitleId = rating.TitleId,
                    Value = null,
                    At = now,
                    Origin = RatingEventOrigins.Import,
                });
            }
        }

        foreach (var item in request.Items)
        {
            if (string.IsNullOrWhiteSpace(item.TmdbType) || item.TmdbId <= 0 || !RatingValues.IsValid(item.Value))
            {
                ignored++;
                continue;
            }

            var tmdbType = item.TmdbType.Trim();
            var title = await db.Titles.FirstOrDefaultAsync(
                t => t.TmdbId == item.TmdbId
                     && t.TmdbType != null
                     && t.TmdbType.ToLower() == tmdbType.ToLower(),
                cancellationToken);
            if (title is null)
            {
                unknown++;
                continue;
            }

            var normalized = RatingValues.Normalize(item.Value);
            var rating = await db.Ratings.FindAsync([profileId, title.Id], cancellationToken);
            if (rating is not null && string.Equals(rating.Value, normalized, StringComparison.Ordinal))
            {
                ignored++;
                continue;
            }

            if (rating is null)
            {
                db.Ratings.Add(new Rating
                {
                    ProfileId = profileId,
                    TitleId = title.Id,
                    Value = normalized,
                    RatedAt = now,
                });
            }
            else
            {
                rating.Value = normalized;
                rating.RatedAt = now;
            }

            db.RatingEvents.Add(new RatingEvent
            {
                ProfileId = profileId,
                TitleId = title.Id,
                Value = normalized,
                At = now,
                Origin = RatingEventOrigins.Import,
            });
            imported++;
        }

        await db.SaveChangesAsync(cancellationToken);
        if (imported > 0 || mode == "replace")
        {
            await ScheduleTasteRebuildAsync(profileId, cancellationToken);
        }

        return new RatingImportResultDto(imported, unknown, ignored);
    }

    public async Task OnboardingAsync(
        int profileId,
        IReadOnlyList<RatingOnboardingItem> items,
        CancellationToken cancellationToken = default)
    {
        await EnsureEnabledAsync(cancellationToken);
        var changed = false;
        var now = clock.UtcNow;

        foreach (var item in items)
        {
            if (item.Value is null || string.IsNullOrWhiteSpace(item.Value))
            {
                continue;
            }

            if (!RatingValues.IsValid(item.Value))
            {
                continue;
            }

            var exists = await db.Titles.AnyAsync(t => t.Id == item.TitleId, cancellationToken);
            if (!exists)
            {
                continue;
            }

            var normalized = RatingValues.Normalize(item.Value);
            var rating = await db.Ratings.FindAsync([profileId, item.TitleId], cancellationToken);
            if (rating is not null && string.Equals(rating.Value, normalized, StringComparison.Ordinal))
            {
                continue;
            }

            if (rating is null)
            {
                db.Ratings.Add(new Rating
                {
                    ProfileId = profileId,
                    TitleId = item.TitleId,
                    Value = normalized,
                    RatedAt = now,
                });
            }
            else
            {
                rating.Value = normalized;
                rating.RatedAt = now;
            }

            db.RatingEvents.Add(new RatingEvent
            {
                ProfileId = profileId,
                TitleId = item.TitleId,
                Value = normalized,
                At = now,
                Origin = RatingEventOrigins.Onboarding,
            });
            changed = true;
        }

        if (!changed)
        {
            return;
        }

        await db.SaveChangesAsync(cancellationToken);
        await ScheduleTasteRebuildAsync(profileId, cancellationToken);
    }

    /// <summary>
    /// A vote rebuilds the voter and every profile still under <c>N0</c>.
    /// A profile already at <c>N_eff ≥ N0</c> keeps its build: the household term is zero there.
    /// </summary>
    private async Task ScheduleTasteRebuildAsync(int profileId, CancellationToken cancellationToken)
    {
        rebuildScheduler.ScheduleDebouncedRebuild(profileId);
        var n0 = await ReadN0Async(cancellationToken);
        var others = await db.Profiles.AsNoTracking()
            .Where(profile => profile.Id != profileId)
            .OrderBy(profile => profile.Id)
            .Select(profile => profile.Id)
            .ToListAsync(cancellationToken);
        if (others.Count == 0)
        {
            return;
        }

        var effective = await db.RecoStates.AsNoTracking()
            .Where(state => state.ProfileId != profileId)
            .Select(state => new { state.ProfileId, state.EffectiveN })
            .ToListAsync(cancellationToken);
        var byProfile = effective.ToDictionary(state => state.ProfileId, state => state.EffectiveN);
        foreach (var otherId in others)
        {
            if (byProfile.GetValueOrDefault(otherId) < n0)
            {
                rebuildScheduler.ScheduleDebouncedRebuild(otherId);
            }
        }
    }

    private async Task<double> ReadN0Async(CancellationToken cancellationToken)
    {
        var raw = await settings.GetRawAsync(SettingsKeys.RecoN0, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return RecoLearning.DefaultN0;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<double>(raw);
        }
        catch (System.Text.Json.JsonException)
        {
            return RecoLearning.DefaultN0;
        }
    }

    private async Task EnsureEnabledAsync(CancellationToken cancellationToken)
    {
        var raw = await settings.GetRawAsync(SettingsKeys.RecoEnabled, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        var enabled = System.Text.Json.JsonSerializer.Deserialize<bool>(raw);
        if (!enabled)
        {
            throw new RecoDisabledException();
        }
    }
}

public sealed class RecoDisabledException : InvalidOperationException
{
    public RecoDisabledException() : base("Recommendations feature is disabled.")
    {
    }
}
