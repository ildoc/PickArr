using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Metadata;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Metadata;

public sealed class EnrichmentService(
    PickarrDbContext db,
    IMetadataProvider metadata,
    IClock clock,
    ILogger<EnrichmentService> logger) : IEnrichmentService
{
    private const int MaxAttempts = 5;

    public async Task EnrichTitleAsync(int titleId, CancellationToken cancellationToken = default)
    {
        var title = await LoadTitleGraphAsync(titleId, cancellationToken)
            ?? throw new InvalidOperationException($"Title '{titleId}' was not found.");

        await EnrichCoreAsync(title, forceRefresh: false, cancellationToken);
    }

    public async Task<EnrichmentBatchResult> EnrichPendingBatchAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var batch = await db.Titles
            .Include(t => t.Localizations)
            .Include(t => t.TitleGenres)
            .Include(t => t.TitleKeywords)
            .Include(t => t.TitleCredits)
            .Include(t => t.Availabilities)
            .ThenInclude(a => a.CatalogSource)
            .Where(t =>
                t.MetadataStatus == MetadataStatuses.Pending ||
                (t.MetadataStatus == MetadataStatuses.Failed && t.MetadataAttempts < MaxAttempts))
            .OrderBy(t => t.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return await ProcessBatchAsync(batch, forceRefresh: false, cancellationToken);
    }

    public async Task<EnrichmentBatchResult> RefreshStaleBatchAsync(
        int limit = 50,
        int olderThanDays = 30,
        CancellationToken cancellationToken = default)
    {
        var cutoff = clock.UtcNow.AddDays(-olderThanDays);
        var batch = await db.Titles
            .Include(t => t.Localizations)
            .Include(t => t.TitleGenres)
            .Include(t => t.TitleKeywords)
            .Include(t => t.TitleCredits)
            .Include(t => t.Availabilities)
            .ThenInclude(a => a.CatalogSource)
            .Where(t =>
                t.MetadataStatus == MetadataStatuses.Ready &&
                t.MetadataFetchedAt != null &&
                t.MetadataFetchedAt < cutoff &&
                t.Availabilities.Any(a => a.RemovedAt == null))
            .OrderBy(t => t.MetadataFetchedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return await ProcessBatchAsync(batch, forceRefresh: true, cancellationToken);
    }

    public async Task<EnrichmentBatchResult> EnrichFeatureBackfillBatchAsync(
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var english = LanguageFallback.English;
        var batch = await db.Titles
            .Include(t => t.Localizations)
            .Include(t => t.TitleGenres)
            .Include(t => t.TitleKeywords)
            .Include(t => t.TitleCredits)
            .Include(t => t.Availabilities)
            .ThenInclude(a => a.CatalogSource)!
            .ThenInclude(s => s!.Provider)
            .Where(t =>
                t.MetadataStatus == MetadataStatuses.Ready
                && t.TmdbId != null
                && t.TmdbType != null
                && t.Availabilities.Any(a =>
                    a.CatalogSource != null
                    && a.CatalogSource.IsEnabled
                    && a.CatalogSource.Provider != null
                    && a.CatalogSource.Provider.IsEnabled)
                && (
                    !t.TitleKeywords.Any()
                    || !t.TitleCredits.Any()
                    || !t.Localizations.Any(l => l.Language == english)
                    || t.OriginCountries == null))
            .OrderBy(t => t.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return await ProcessBatchAsync(batch, forceRefresh: true, cancellationToken);
    }

    private async Task<Title?> LoadTitleGraphAsync(int titleId, CancellationToken cancellationToken) =>
        await db.Titles
            .Include(t => t.Localizations)
            .Include(t => t.TitleGenres)
            .Include(t => t.TitleKeywords)
            .Include(t => t.TitleCredits)
            .Include(t => t.Availabilities)
            .ThenInclude(a => a.CatalogSource)
            .FirstOrDefaultAsync(t => t.Id == titleId, cancellationToken);

    private async Task<EnrichmentBatchResult> ProcessBatchAsync(
        IReadOnlyList<Title> batch,
        bool forceRefresh,
        CancellationToken cancellationToken)
    {
        var ready = 0;
        var failed = 0;
        var unmapped = 0;
        string? lastError = null;
        foreach (var title in batch)
        {
            try
            {
                await EnrichCoreAsync(title, forceRefresh, cancellationToken);
                switch (title.MetadataStatus)
                {
                    case MetadataStatuses.Ready: ready++; break;
                    case MetadataStatuses.Unmapped: unmapped++; break;
                    default: failed++; break;
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Enrichment failed for title {TitleId}", title.Id);
                lastError = ex.Message;
                title.MetadataAttempts++;
                title.MetadataStatus = MetadataStatuses.Failed;
                title.UpdatedAt = clock.UtcNow;
                failed++;
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return new EnrichmentBatchResult(batch.Count, ready, failed, unmapped, lastError);
    }

    private async Task EnrichCoreAsync(Title title, bool forceRefresh, CancellationToken cancellationToken)
    {
        _ = forceRefresh;

        if (!await EnsureTmdbIdsAsync(title, cancellationToken))
        {
            title.MetadataStatus = MetadataStatuses.Unmapped;
            title.UpdatedAt = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        var languages = ResolveLanguages(title);
        TmdbTitleDetails? primary = null;
        foreach (var language in languages)
        {
            var details = await FetchDetailsAsync(title, language, cancellationToken);
            if (details is null)
            {
                continue;
            }

            primary ??= details;
            UpsertLocalization(title, language, details);

            if (LanguageFallback.NeedsEnglishFallback(details.Title, details.Overview) &&
                !string.Equals(language, LanguageFallback.English, StringComparison.OrdinalIgnoreCase))
            {
                var en = await FetchDetailsAsync(title, LanguageFallback.English, cancellationToken);
                if (en is not null)
                {
                    UpsertLocalization(title, LanguageFallback.English, en);
                    primary ??= en;
                }
            }
        }

        // BR-REC-09 / §11.2: ensure en-US row even when source language text is complete.
        if (!HasEnglishLocalization(title))
        {
            var en = await FetchDetailsAsync(title, LanguageFallback.English, cancellationToken);
            if (en is not null)
            {
                UpsertLocalization(title, LanguageFallback.English, en);
                primary ??= en;
            }
        }

        if (primary is null)
        {
            // TMDB 404 / unknown id — not a credential problem. Stop retrying as Failed.
            logger.LogInformation(
                "TMDB has no details for title {TitleId} ({TmdbType}/{TmdbId}); marking Unmapped",
                title.Id,
                title.TmdbType,
                title.TmdbId);
            title.MetadataStatus = MetadataStatuses.Unmapped;
            title.UpdatedAt = clock.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        ApplyDetails(title, primary);
        await UpsertGenresAsync(title, primary, languages[0], cancellationToken);
        await UpsertKeywordsAsync(title, primary, cancellationToken);
        await UpsertCreditsAsync(title, primary, cancellationToken);
        title.MetadataStatus = MetadataStatuses.Ready;
        title.MetadataFetchedAt = clock.UtcNow;
        title.MetadataAttempts = 0;
        title.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool HasEnglishLocalization(Title title) =>
        title.Localizations.Any(l =>
            string.Equals(l.Language, LanguageFallback.English, StringComparison.OrdinalIgnoreCase));

    private async Task<bool> EnsureTmdbIdsAsync(Title title, CancellationToken cancellationToken)
    {
        if (title.TmdbId is > 0 && !string.IsNullOrWhiteSpace(title.TmdbType))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(title.ImdbId))
        {
            return false;
        }

        var found = await metadata.FindByImdbAsync(title.ImdbId, cancellationToken);
        if (found is null)
        {
            return false;
        }

        title.TmdbId = found.TmdbId;
        title.TmdbType = found.TmdbType;
        return true;
    }

    private async Task<TmdbTitleDetails?> FetchDetailsAsync(
        Title title,
        string language,
        CancellationToken cancellationToken)
    {
        var tmdbId = title.TmdbId!.Value;
        var isMovie = string.Equals(title.TmdbType, "movie", StringComparison.OrdinalIgnoreCase);
        var details = isMovie
            ? await metadata.GetMovieAsync(tmdbId, language, cancellationToken)
            : await metadata.GetTvAsync(tmdbId, language, cancellationToken);
        if (details is not null)
        {
            return details;
        }

        // Watchmode occasionally stores the wrong TMDB media type; try the other endpoint once.
        var alternate = isMovie
            ? await metadata.GetTvAsync(tmdbId, language, cancellationToken)
            : await metadata.GetMovieAsync(tmdbId, language, cancellationToken);
        if (alternate is null)
        {
            return null;
        }

        var newType = isMovie ? "tv" : "movie";
        var conflict = await db.Titles.AsNoTracking().AnyAsync(
            t => t.Id != title.Id
                 && t.TmdbId == tmdbId
                 && t.TmdbType == newType,
            cancellationToken);
        if (conflict)
        {
            logger.LogWarning(
                "Title {TitleId} resolves as {NewType}/{TmdbId} but that key already exists; leaving type unchanged",
                title.Id,
                newType,
                tmdbId);
            return alternate;
        }

        logger.LogInformation(
            "Correcting TMDB type for title {TitleId}: {OldType} → {NewType} (id {TmdbId})",
            title.Id,
            title.TmdbType,
            newType,
            tmdbId);
        title.TmdbType = newType;
        title.MediaType = isMovie ? MediaTypes.Series : MediaTypes.Movie;
        title.Category = isMovie ? TitleCategories.Series : TitleCategories.Movie;
        return alternate;
    }

    private static List<string> ResolveLanguages(Title title)
    {
        var langs = title.Availabilities
            .Where(a => a.RemovedAt is null && a.CatalogSource is not null)
            .Select(a => a.CatalogSource!.MetadataLanguage)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (langs.Count == 0)
        {
            langs.Add(LanguageFallback.English);
        }

        return langs;
    }

    private void UpsertLocalization(Title title, string language, TmdbTitleDetails details)
    {
        var name = details.Title ?? details.OriginalTitle ?? title.OriginalTitle ?? $"Title {title.Id}";
        var existing = title.Localizations.FirstOrDefault(l =>
            string.Equals(l.Language, language, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            title.Localizations.Add(new TitleLocalization
            {
                TitleId = title.Id,
                Language = language,
                Name = name,
                NameNormalized = TextNormalizer.NormalizeName(name),
                Overview = details.Overview,
                Tagline = details.Tagline,
                FetchedAt = clock.UtcNow,
            });
            return;
        }

        existing.Name = name;
        existing.NameNormalized = TextNormalizer.NormalizeName(name);
        existing.Overview = details.Overview;
        existing.Tagline = details.Tagline;
        existing.FetchedAt = clock.UtcNow;
    }

    private static void ApplyDetails(Title title, TmdbTitleDetails details)
    {
        title.OriginalTitle = details.OriginalTitle ?? details.Title ?? title.OriginalTitle;
        title.OriginalTitleNormalized = TextNormalizer.NormalizeName(title.OriginalTitle);
        title.ReleaseYear = details.Year ?? title.ReleaseYear;
        title.RuntimeMinutes = details.RuntimeMinutes ?? title.RuntimeMinutes;
        title.SeasonCount = details.SeasonCount ?? title.SeasonCount;
        title.EpisodeCount = details.EpisodeCount ?? title.EpisodeCount;
        title.VoteTmdb = details.VoteAverage;
        title.VoteTmdbCount = details.VoteCount;
        title.Popularity = details.Popularity;
        title.OriginalLanguage = details.OriginalLanguage;
        title.PosterPath = details.PosterPath;
        title.BackdropPath = details.BackdropPath;
        if (!string.IsNullOrWhiteSpace(details.ImdbId))
        {
            title.ImdbId = details.ImdbId;
        }

        if (details.TvdbId is not null)
        {
            title.TvdbId = details.TvdbId;
        }

        if (details.Cast is { Count: > 0 })
        {
            title.CastJson = JsonSerializer.Serialize(details.Cast);
        }

        if (details.OriginCountries is { Count: > 0 })
        {
            title.OriginCountries = JsonSerializer.Serialize(details.OriginCountries);
        }

        if (details.Seasons is { Count: > 0 })
        {
            title.SeasonsJson = SeriesSeasonJson.Write(
                SeriesSeasonJson.MergeSummaries(title.SeasonsJson, details.Seasons));
        }

        var isStandUp = details.Keywords.Any(k => TmdbStandUp.IsStandUpKeyword(k.Id, k.Name));
        title.Category = isStandUp
            ? TitleCategories.StandUp
            : title.MediaType == MediaTypes.Movie ? TitleCategories.Movie : TitleCategories.Series;
    }

    private async Task UpsertGenresAsync(
        Title title,
        TmdbTitleDetails details,
        string language,
        CancellationToken cancellationToken)
    {
        foreach (var genre in details.Genres)
        {
            var entity = await db.Genres.FindAsync([genre.Id], cancellationToken);
            if (entity is null)
            {
                entity = new Genre { Id = genre.Id };
                db.Genres.Add(entity);
            }

            var loc = await db.GenreLocalizations.FindAsync([genre.Id, language], cancellationToken);
            if (loc is null)
            {
                db.GenreLocalizations.Add(new GenreLocalization
                {
                    GenreId = genre.Id,
                    Language = language,
                    Name = genre.Name,
                });
            }
            else
            {
                loc.Name = genre.Name;
            }

            if (!title.TitleGenres.Any(tg => tg.GenreId == genre.Id))
            {
                title.TitleGenres.Add(new TitleGenre { TitleId = title.Id, GenreId = genre.Id });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertKeywordsAsync(
        Title title,
        TmdbTitleDetails details,
        CancellationToken cancellationToken)
    {
        foreach (var keyword in details.Keywords.Where(k => k.Id > 0 && !string.IsNullOrWhiteSpace(k.Name)))
        {
            var entity = await db.Keywords.FindAsync([keyword.Id], cancellationToken);
            if (entity is null)
            {
                db.Keywords.Add(new Keyword { Id = keyword.Id, Name = keyword.Name.Trim() });
            }
            else if (!string.Equals(entity.Name, keyword.Name.Trim(), StringComparison.Ordinal))
            {
                entity.Name = keyword.Name.Trim();
            }

            if (!title.TitleKeywords.Any(tk => tk.KeywordId == keyword.Id))
            {
                title.TitleKeywords.Add(new TitleKeyword { TitleId = title.Id, KeywordId = keyword.Id });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task UpsertCreditsAsync(
        Title title,
        TmdbTitleDetails details,
        CancellationToken cancellationToken)
    {
        if (details.Credits is null || details.Credits.Count == 0)
        {
            return;
        }

        foreach (var credit in details.Credits.Where(c => c.PersonId > 0 && !string.IsNullOrWhiteSpace(c.Name)))
        {
            var person = await db.People.FindAsync([credit.PersonId], cancellationToken);
            if (person is null)
            {
                db.People.Add(new Person { Id = credit.PersonId, Name = credit.Name.Trim() });
            }
            else if (!string.Equals(person.Name, credit.Name.Trim(), StringComparison.Ordinal))
            {
                person.Name = credit.Name.Trim();
            }

            var existing = title.TitleCredits.FirstOrDefault(tc =>
                tc.PersonId == credit.PersonId
                && string.Equals(tc.Role, credit.Role, StringComparison.Ordinal));
            if (existing is null)
            {
                title.TitleCredits.Add(new TitleCredit
                {
                    TitleId = title.Id,
                    PersonId = credit.PersonId,
                    Role = credit.Role,
                    Order = credit.Order,
                });
            }
            else
            {
                existing.Order = credit.Order;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
