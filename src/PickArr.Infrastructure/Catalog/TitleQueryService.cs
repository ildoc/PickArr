using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Arr;
using PickArr.Core.Catalog;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Metadata;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Persistence;

namespace PickArr.Infrastructure.Catalog;

public sealed class TitleQueryService(
    PickArrDbContext db,
    ISettingsStore settings,
    IClock clock) : ITitleQueryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<CatalogSourceDto>> ListSourcesAsync(CancellationToken cancellationToken = default)
    {
        var staleDays = await GetIntSettingAsync(SettingsKeys.CatalogStaleWarningDays, 21, cancellationToken);
        var now = clock.UtcNow;
        var sources = await db.CatalogSources
            .AsNoTracking()
            .Include(s => s.Provider)
            .Where(s => s.IsEnabled && s.Provider != null && s.Provider.IsEnabled)
            .OrderBy(s => s.Provider!.DisplayName)
            .ThenBy(s => s.CountryCode)
            .ToListAsync(cancellationToken);

        return sources.Select(s =>
        {
            var stale = s.LastFullAt is null
                || s.LastFullAt < now.AddDays(-staleDays);
            return new CatalogSourceDto(
                s.Id,
                s.Provider!.Key,
                s.Provider.DisplayName,
                s.CountryCode,
                s.MetadataLanguage,
                s.BaselineCompletedAt is not null,
                s.LastFullAt,
                stale);
        }).ToList();
    }

    public async Task<PagedTitlesResult> QueryTitlesAsync(
        int sourceId,
        TitleQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 48 : request.PageSize, 1, 100);
        var arrFilter = await ResolveArrFilterAsync(cancellationToken);

        if (arrFilter.AllowedMediaTypes.Count == 0)
        {
            return new PagedTitlesResult(
                page,
                pageSize,
                Total: 0,
                Items: [],
                EmptyReason: ArrMediaFilter.EmptyReasonArrNotConfigured,
                ArrFilter: arrFilter);
        }

        var source = await db.CatalogSources.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken);
        var favoriteOnly = request.Favorite == true;
        if (source is null && !favoriteOnly)
        {
            return new PagedTitlesResult(page, pageSize, 0, [], ArrFilter: arrFilter);
        }

        var newWindow = await GetIntSettingAsync(SettingsKeys.CatalogNewWindowDays, 14, cancellationToken);
        var leavingWindow = await GetIntSettingAsync(SettingsKeys.CatalogLeavingWindowDays, 30, cancellationToken);
        var removedWindow = await GetIntSettingAsync(SettingsKeys.CatalogRemovedWindowDays, 30, cancellationToken);
        var now = clock.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var leavingUntil = today.AddDays(leavingWindow);
        var newCutoff = now.AddDays(-newWindow);
        var removedCutoff = now.AddDays(-removedWindow);
        var view = (request.View ?? "all").Trim().ToLowerInvariant();
        var includeRemoved = request.IncludeRemoved || view == "removed" || favoriteOnly;

        var query = db.Titles.AsNoTracking()
            .Where(t => arrFilter.AllowedMediaTypes.Contains(t.MediaType));

        if (favoriteOnly)
        {
            query = query.Where(t => t.Favorite != null);
        }
        else
        {
            query = query.Where(t => t.Availabilities.Any(a => a.CatalogSourceId == sourceId));
        }

        // Availability-scoped filters (except favorite-only orphans).
        query = view switch
        {
            "new" => query.Where(t => t.Availabilities.Any(a =>
                a.CatalogSourceId == sourceId
                && a.RemovedAt == null
                && !a.IsBaseline
                && a.AddedAt != null
                && a.AddedAt >= newCutoff)),
            "leaving" => query.Where(t => t.Availabilities.Any(a =>
                a.CatalogSourceId == sourceId
                && a.RemovedAt == null
                && a.LeavingDate != null
                && a.LeavingDate >= today
                && a.LeavingDate <= leavingUntil)),
            "removed" => query.Where(t => t.Availabilities.Any(a =>
                a.CatalogSourceId == sourceId
                && a.RemovedAt != null
                && a.RemovedAt >= removedCutoff)),
            _ when !includeRemoved => query.Where(t => t.Availabilities.Any(a =>
                a.CatalogSourceId == sourceId && a.RemovedAt == null)),
            _ => query,
        };

        if (request.Types is { Count: > 0 })
        {
            var categories = request.Types
                .Select(MapTypeToCategory)
                .Where(c => c is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (categories.Count > 0)
            {
                query = query.Where(t => categories.Contains(t.Category));
            }
        }

        if (request.Genres is { Count: > 0 })
        {
            var genreIds = request.Genres.ToList();
            query = query.Where(t => t.TitleGenres.Any(tg => genreIds.Contains(tg.GenreId)));
        }

        if (request.YearFrom is { } yearFrom)
        {
            query = query.Where(t => t.ReleaseYear >= yearFrom);
        }

        if (request.YearTo is { } yearTo)
        {
            query = query.Where(t => t.ReleaseYear <= yearTo);
        }

        if (request.MinRating is { } minRating)
        {
            query = query.Where(t => t.VoteTmdb >= minRating);
        }

        if (request.ArrStates is { Count: > 0 })
        {
            var apiStates = request.ArrStates
                .Select(s => s.Trim().ToLowerInvariant())
                .ToHashSet(StringComparer.Ordinal);
            var wantNotInLibrary = apiStates.Contains("notinlibrary");
            var storageStates = apiStates
                .Where(s => s != "notinlibrary")
                .Select(s => ArrKindExtensions.ParseState(s).ToString())
                .ToList();

            query = query.Where(t =>
                (wantNotInLibrary && !db.ArrLibraryItems.Any(a => a.TitleId == t.Id))
                || (storageStates.Count > 0
                    && db.ArrLibraryItems.Any(a => a.TitleId == t.Id && storageStates.Contains(a.State))));
        }

        string? searchTerm = null;
        if (!string.IsNullOrWhiteSpace(request.Q))
        {
            searchTerm = TextNormalizer.NormalizeName(request.Q);
            if (searchTerm.Length > 0)
            {
                var term = searchTerm;
                if (favoriteOnly)
                {
                    query = query.Where(t =>
                        (t.OriginalTitleNormalized != null && t.OriginalTitleNormalized.Contains(term))
                        || t.Localizations.Any(l => l.NameNormalized.Contains(term)));
                }
                else
                {
                    var lang = source!.MetadataLanguage;
                    query = query.Where(t =>
                        (t.OriginalTitleNormalized != null
                         && t.OriginalTitleNormalized.Contains(term))
                        || t.Localizations.Any(l =>
                            (l.Language == lang || l.Language == LanguageFallback.English)
                            && l.NameNormalized.Contains(term)));
                }
            }
        }

        var sort = (request.Sort ?? "added").Trim().ToLowerInvariant();
        var desc = !string.Equals(request.Dir, "asc", StringComparison.OrdinalIgnoreCase);
        if (sort == "relevance" && searchTerm is null)
        {
            sort = "added";
        }

        query = ApplySort(query, sort, desc, source?.Id ?? 0, searchTerm, favoriteOnly);

        var total = await query.CountAsync(cancellationToken);
        var pageIds = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var titles = await db.Titles.AsNoTracking()
            .Where(t => pageIds.Contains(t.Id))
            .Include(t => t.Favorite)
            .Include(t => t.Localizations)
            .Include(t => t.TitleGenres).ThenInclude(tg => tg.Genre!).ThenInclude(g => g.Localizations)
            .Include(t => t.Availabilities).ThenInclude(a => a.CatalogSource!).ThenInclude(s => s.Provider)
            .ToListAsync(cancellationToken);
        var byId = titles.ToDictionary(t => t.Id);
        var arrByTitle = await db.ArrLibraryItems.AsNoTracking()
            .Where(a => a.TitleId != null && pageIds.Contains(a.TitleId.Value))
            .ToDictionaryAsync(a => a.TitleId!.Value, cancellationToken);
        var items = pageIds
            .Where(id => byId.ContainsKey(id))
            .Select(id => MapListItem(
                byId[id],
                source,
                newCutoff,
                arrByTitle.GetValueOrDefault(id)))
            .ToList();

        string? emptyReason = null;
        if (source is not null && total == 0 && IsDefaultBrowse(request) && NeverImported(source))
        {
            var importing = await db.SyncRuns.AsNoTracking().AnyAsync(
                r => r.CatalogSourceId == source.Id
                     && r.Status == SyncRunStatuses.Running
                     && (r.Kind == SyncRunKinds.CatalogIncremental || r.Kind == SyncRunKinds.CatalogFull),
                cancellationToken);
            emptyReason = importing ? CatalogEmptyReasons.Importing : CatalogEmptyReasons.NotImported;
        }

        return new PagedTitlesResult(page, pageSize, total, items, emptyReason, arrFilter);
    }

    private static bool NeverImported(CatalogSource source) =>
        source.BaselineCompletedAt is null
        && source.LastIncrementalAt is null
        && source.LastFullAt is null;

    private static bool IsDefaultBrowse(TitleQueryRequest request) =>
        string.IsNullOrWhiteSpace(request.Q)
        && (string.IsNullOrWhiteSpace(request.View) || string.Equals(request.View, "all", StringComparison.OrdinalIgnoreCase))
        && request.Favorite != true
        && request.Types is not { Count: > 0 }
        && request.Genres is not { Count: > 0 }
        && request.YearFrom is null
        && request.YearTo is null
        && request.ArrStates is not { Count: > 0 }
        && request.MinRating is null;

    public async Task<IReadOnlyList<GenreCountDto>> ListGenresAsync(
        int sourceId,
        bool favoritesOnly,
        CancellationToken cancellationToken = default)
    {
        var arrFilter = await ResolveArrFilterAsync(cancellationToken);
        if (arrFilter.AllowedMediaTypes.Count == 0)
        {
            return [];
        }

        var source = await db.CatalogSources.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken);
        if (source is null && !favoritesOnly)
        {
            return [];
        }

        var lang = source?.MetadataLanguage ?? LanguageFallback.English;
        var rows = await db.TitleGenres.AsNoTracking()
            .Where(tg =>
                arrFilter.AllowedMediaTypes.Contains(tg.Title!.MediaType)
                && (favoritesOnly
                    ? tg.Title.Favorite != null
                    : tg.Title.Availabilities.Any(a => a.CatalogSourceId == sourceId && a.RemovedAt == null)))
            .GroupBy(tg => tg.GenreId)
            .Select(g => new { GenreId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        var ids = rows.Select(r => r.GenreId).ToList();
        var localizations = await db.GenreLocalizations.AsNoTracking()
            .Where(l => ids.Contains(l.GenreId)
                        && (l.Language == lang || l.Language == LanguageFallback.English))
            .ToListAsync(cancellationToken);

        return rows
            .Select(r =>
            {
                var name = localizations
                                .Where(l => l.GenreId == r.GenreId)
                                .OrderByDescending(l => l.Language == lang)
                                .Select(l => l.Name)
                                .FirstOrDefault()
                            ?? $"Genre {r.GenreId}";
                return new GenreCountDto(r.GenreId, name, r.Count);
            })
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<CatalogTypeCountDto>> ListTypesAsync(
        int sourceId,
        bool favoritesOnly,
        CancellationToken cancellationToken = default)
    {
        var arrFilter = await ResolveArrFilterAsync(cancellationToken);
        if (arrFilter.AllowedMediaTypes.Count == 0)
        {
            return [];
        }

        var query = db.Titles.AsNoTracking()
            .Where(t => arrFilter.AllowedMediaTypes.Contains(t.MediaType));
        query = favoritesOnly
            ? query.Where(t => t.Favorite != null)
            : query.Where(t => t.Availabilities.Any(a => a.CatalogSourceId == sourceId && a.RemovedAt == null));

        var rows = await query
            .GroupBy(t => t.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new CatalogTypeCountDto(ToApiCategory(row.Category), row.Count))
            .OrderBy(row => TypeSort(row.Type))
            .ThenBy(row => row.Type, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int TypeSort(string type) => type switch
    {
        "movie" => 0,
        "series" => 1,
        "standup" => 2,
        _ => 3,
    };

    public async Task<TitleDetailDto?> GetTitleAsync(
        int titleId,
        int? sourceId,
        CancellationToken cancellationToken = default)
    {
        var title = await db.Titles.AsNoTracking()
            .Include(t => t.Favorite)
            .Include(t => t.Localizations)
            .Include(t => t.TitleGenres).ThenInclude(tg => tg.Genre!).ThenInclude(g => g.Localizations)
            .Include(t => t.Availabilities).ThenInclude(a => a.CatalogSource)
            .FirstOrDefaultAsync(t => t.Id == titleId, cancellationToken);
        if (title is null)
        {
            return null;
        }

        var preferredLang = LanguageFallback.English;
        if (sourceId is { } sid)
        {
            preferredLang = await db.CatalogSources.AsNoTracking()
                .Where(s => s.Id == sid)
                .Select(s => s.MetadataLanguage)
                .FirstOrDefaultAsync(cancellationToken) ?? LanguageFallback.English;
        }
        else if (title.Availabilities.FirstOrDefault()?.CatalogSource?.MetadataLanguage is { } lang)
        {
            preferredLang = lang;
        }

        var (name, overview) = ResolveLocalization(title, preferredLang);
        var genres = MapGenres(title, preferredLang);
        var sonarr = await IsSonarrConfiguredAsync(cancellationToken);
        var radarr = await IsRadarrConfiguredAsync(cancellationToken);
        var arrItem = await db.ArrLibraryItems.AsNoTracking()
            .Include(a => a.Seasons)
            .FirstOrDefaultAsync(a => a.TitleId == title.Id, cancellationToken);
        var arr = BuildArrDetail(title, arrItem, sonarr, radarr);

        return new TitleDetailDto(
            title.Id,
            name,
            title.OriginalTitle,
            title.ReleaseYear,
            ToApiCategory(title.Category),
            overview,
            title.RuntimeMinutes,
            title.SeasonCount,
            title.EpisodeCount,
            genres,
            TmdbImageUrls.Build(title.PosterPath, "w500"),
            TmdbImageUrls.Build(title.BackdropPath, "w780"),
            new TitleRatingsDto(title.VoteTmdb, title.VoteTmdbCount, Imdb: null),
            new TitleLinksDto(
                title.TmdbId is { } tmdbId && title.TmdbType is { } tmdbType
                    ? $"https://www.themoviedb.org/{tmdbType}/{tmdbId}"
                    : null,
                string.IsNullOrWhiteSpace(title.ImdbId) ? null : $"https://www.imdb.com/title/{title.ImdbId}/",
                title.TvdbId is { } tvdb ? $"https://thetvdb.com/?tab=series&id={tvdb}" : null),
            title.Favorite is not null,
            ToApiMetadataStatus(title.MetadataStatus),
            title.Availabilities
                .OrderBy(a => a.CatalogSource?.CountryCode)
                .Select(a => new TitleAvailabilityDto(
                    a.CatalogSourceId,
                    a.CatalogSource?.CountryCode ?? "",
                    a.AddedAt,
                    a.RemovedAt,
                    a.LeavingDate))
                .ToList(),
            arr,
            ReadCast(title.CastJson));
    }

    private static IReadOnlyList<string> ReadCast(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }

    public Task<PagedTitlesResult> QueryFavoritesAsync(
        TitleQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var favorites = request with
        {
            Favorite = true,
            IncludeRemoved = true,
            View = "all",
        };
        return QueryTitlesAsync(0, favorites, cancellationToken);
    }

    public Task<IReadOnlyList<GenreCountDto>> ListFavoriteGenresAsync(
        CancellationToken cancellationToken = default) =>
        ListGenresAsync(0, favoritesOnly: true, cancellationToken);

    public Task<IReadOnlyList<CatalogTypeCountDto>> ListFavoriteTypesAsync(
        CancellationToken cancellationToken = default) =>
        ListTypesAsync(0, favoritesOnly: true, cancellationToken);

    private TitleListItemDto MapListItem(
        Title title,
        CatalogSource? source,
        DateTime newCutoff,
        ArrLibraryItem? arrItem)
    {
        var language = source?.MetadataLanguage ?? LanguageFallback.English;
        var (name, _) = ResolveLocalization(title, language);
        var availability = source is null
            ? null
            : title.Availabilities.FirstOrDefault(a => a.CatalogSourceId == source.Id);
        AvailabilitySummaryDto? availabilityDto = null;
        if (availability is not null)
        {
            var isNew = availability.RemovedAt is null
                        && !availability.IsBaseline
                        && availability.AddedAt is { } added
                        && added >= newCutoff;
            availabilityDto = new AvailabilitySummaryDto(
                availability.AddedAt,
                availability.RemovedAt,
                availability.LeavingDate,
                isNew);
        }

        return new TitleListItemDto(
            title.Id,
            name,
            title.OriginalTitle,
            title.ReleaseYear,
            ToApiCategory(title.Category),
            TmdbImageUrls.Build(title.PosterPath, "w342"),
            MapGenres(title, language),
            title.VoteTmdb,
            RatingImdb: null,
            title.Favorite is not null,
            availabilityDto,
            BuildArrSummary(title.MediaType, arrItem),
            MapPlaces(title));
    }

    private static IReadOnlyList<CatalogPlaceDto> MapPlaces(Title title) =>
        title.Availabilities
            .Where(a => a.RemovedAt == null && a.CatalogSource is not null)
            .OrderBy(a => a.CatalogSource!.Provider?.DisplayName)
            .ThenBy(a => a.CatalogSource!.CountryCode)
            .Select(a => new CatalogPlaceDto(
                a.CatalogSource!.Provider?.DisplayName ?? "",
                a.CatalogSource.CountryCode))
            .ToList();

    private static ArrSummaryDto BuildArrSummary(string mediaType, ArrLibraryItem? arrItem)
    {
        var kind = mediaType == MediaTypes.Series ? "sonarr" : "radarr";
        if (arrItem is null)
        {
            return new ArrSummaryDto(kind, "notInLibrary");
        }

        return new ArrSummaryDto(
            arrItem.Kind.ToLowerInvariant(),
            ArrKindExtensions.ParseState(arrItem.State).ToApi(),
            arrItem.Monitored,
            arrItem.QueueProgress);
    }

    private static ArrDetailDto BuildArrDetail(
        Title title,
        ArrLibraryItem? arrItem,
        bool sonarrConfigured,
        bool radarrConfigured)
    {
        var isSeries = title.MediaType == MediaTypes.Series;
        var kind = isSeries ? "sonarr" : "radarr";
        var configured = isSeries ? sonarrConfigured : radarrConfigured;

        if (arrItem is not null)
        {
            var fileCount = arrItem.EpisodeFileCount
                ?? (arrItem.HasFile == true ? 1 : arrItem.HasFile == false ? 0 : null);
            return new ArrDetailDto(
                Kind: arrItem.Kind.ToLowerInvariant(),
                ArrId: arrItem.ArrId,
                State: ArrKindExtensions.ParseState(arrItem.State).ToApi(),
                Monitored: arrItem.Monitored,
                Progress: arrItem.QueueProgress,
                SizeOnDisk: arrItem.SizeOnDisk,
                Path: arrItem.Path,
                FileCount: fileCount,
                SyncedAt: arrItem.SyncedAt,
                Seasons: arrItem.Seasons
                    .OrderBy(s => s.SeasonNumber)
                    .Select(s => new ArrSeasonDto(
                        s.SeasonNumber,
                        s.Monitored,
                        ArrKindExtensions.ParseState(s.State).ToApi(),
                        s.EpisodeFileCount,
                        s.EpisodeCount,
                        s.TotalEpisodeCount))
                    .ToList(),
                CanAdd: false,
                CanDelete: true,
                AddBlockedReason: "alreadyInLibrary",
                ExternalUrl: null);
        }

        string? blocked = null;
        var canAdd = configured;
        if (!configured)
        {
            blocked = "notConfigured";
            canAdd = false;
        }
        else if (isSeries && title.TvdbId is null && title.TmdbId is null && string.IsNullOrWhiteSpace(title.ImdbId))
        {
            blocked = "missingTvdbId";
            canAdd = false;
        }
        else if (!isSeries && title.TmdbId is null)
        {
            blocked = "missingTmdbId";
            canAdd = false;
        }

        return new ArrDetailDto(
            Kind: kind,
            ArrId: null,
            State: "notInLibrary",
            Monitored: null,
            Progress: null,
            SizeOnDisk: null,
            Path: null,
            FileCount: null,
            SyncedAt: null,
            Seasons: null,
            CanAdd: canAdd,
            CanDelete: false,
            AddBlockedReason: blocked,
            ExternalUrl: null);
    }

    private static (string Name, string? Overview) ResolveLocalization(Title title, string preferredLang)
    {
        var preferred = title.Localizations.FirstOrDefault(l =>
            string.Equals(l.Language, preferredLang, StringComparison.OrdinalIgnoreCase));
        if (preferred is not null)
        {
            return (preferred.Name, preferred.Overview);
        }

        var english = title.Localizations.FirstOrDefault(l =>
            string.Equals(l.Language, LanguageFallback.English, StringComparison.OrdinalIgnoreCase));
        if (english is not null)
        {
            return (english.Name, english.Overview);
        }

        var any = title.Localizations.FirstOrDefault();
        if (any is not null)
        {
            return (any.Name, any.Overview);
        }

        return (title.OriginalTitle ?? $"Title {title.Id}", null);
    }

    private static IReadOnlyList<GenreRefDto> MapGenres(Title title, string preferredLang) =>
        title.TitleGenres
            .Select(tg =>
            {
                var name = tg.Genre?.Localizations
                               .Where(l => l.Language == preferredLang || l.Language == LanguageFallback.English)
                               .OrderByDescending(l => l.Language == preferredLang)
                               .Select(l => l.Name)
                               .FirstOrDefault()
                           ?? $"Genre {tg.GenreId}";
                return new GenreRefDto(tg.GenreId, name);
            })
            .OrderBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static IQueryable<Title> ApplySort(
        IQueryable<Title> query,
        string sort,
        bool desc,
        int sourceId,
        string? searchTerm,
        bool favoriteOnly)
    {
        if (favoriteOnly && sort is not ("year" or "rating" or "title" or "relevance"))
        {
            return desc
                ? query.OrderByDescending(t => t.Favorite!.CreatedAt).ThenBy(t => t.Id)
                : query.OrderBy(t => t.Favorite!.CreatedAt).ThenBy(t => t.Id);
        }

        return sort switch
        {
            "year" => desc
                ? query.OrderByDescending(t => t.ReleaseYear).ThenBy(t => t.Id)
                : query.OrderBy(t => t.ReleaseYear).ThenBy(t => t.Id),
            "rating" => desc
                ? query.OrderByDescending(t => t.VoteTmdb).ThenBy(t => t.Id)
                : query.OrderBy(t => t.VoteTmdb).ThenBy(t => t.Id),
            "title" => desc
                ? query.OrderByDescending(t => t.OriginalTitleNormalized).ThenBy(t => t.Id)
                : query.OrderBy(t => t.OriginalTitleNormalized).ThenBy(t => t.Id),
            "relevance" when searchTerm is not null && desc => query
                .OrderByDescending(t =>
                    t.OriginalTitleNormalized != null && t.OriginalTitleNormalized.StartsWith(searchTerm)
                    || t.Localizations.Any(l => l.NameNormalized.StartsWith(searchTerm)))
                .ThenBy(t => t.OriginalTitleNormalized)
                .ThenBy(t => t.Id),
            "relevance" when searchTerm is not null => query
                .OrderBy(t =>
                    t.OriginalTitleNormalized != null && t.OriginalTitleNormalized.StartsWith(searchTerm)
                    || t.Localizations.Any(l => l.NameNormalized.StartsWith(searchTerm)))
                .ThenByDescending(t => t.OriginalTitleNormalized)
                .ThenByDescending(t => t.Id),
            _ => desc
                ? query.OrderByDescending(t => t.Availabilities
                        .Where(a => a.CatalogSourceId == sourceId)
                        .Select(a => (DateTime?)(a.AddedAt ?? a.FirstSeenAt))
                        .FirstOrDefault())
                    .ThenBy(t => t.Id)
                : query.OrderBy(t => t.Availabilities
                        .Where(a => a.CatalogSourceId == sourceId)
                        .Select(a => (DateTime?)(a.AddedAt ?? a.FirstSeenAt))
                        .FirstOrDefault())
                    .ThenBy(t => t.Id),
        };
    }

    private async Task<ArrFilterInfoDto> ResolveArrFilterAsync(CancellationToken cancellationToken)
    {
        var sonarr = await IsSonarrConfiguredAsync(cancellationToken);
        var radarr = await IsRadarrConfiguredAsync(cancellationToken);
        return new ArrFilterInfoDto(
            sonarr,
            radarr,
            ArrMediaFilter.AllowedMediaTypes(sonarr, radarr));
    }

    private async Task<bool> IsSonarrConfiguredAsync(CancellationToken cancellationToken) =>
        ArrMediaFilter.IsConfigured(
            await GetStringSettingAsync(SettingsKeys.SonarrUrl, cancellationToken),
            await settings.IsSetAsync(SettingsKeys.SonarrApiKey, cancellationToken));

    private async Task<bool> IsRadarrConfiguredAsync(CancellationToken cancellationToken) =>
        ArrMediaFilter.IsConfigured(
            await GetStringSettingAsync(SettingsKeys.RadarrUrl, cancellationToken),
            await settings.IsSetAsync(SettingsKeys.RadarrApiKey, cancellationToken));

    private async Task<int> GetIntSettingAsync(string key, int defaultValue, CancellationToken cancellationToken)
    {
        var raw = await settings.GetRawAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        try
        {
            return JsonSerializer.Deserialize<int>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                ? n
                : defaultValue;
        }
    }

    private async Task<string?> GetStringSettingAsync(string key, CancellationToken cancellationToken)
    {
        var raw = await settings.GetRawAsync(key, cancellationToken);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<string>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            return raw.Trim('"');
        }
    }

    private static string? MapTypeToCategory(string type) => type.Trim().ToLowerInvariant() switch
    {
        "movie" => TitleCategories.Movie,
        "series" => TitleCategories.Series,
        "standup" or "stand-up" or "stand_up" => TitleCategories.StandUp,
        _ => null,
    };

    private static string ToApiCategory(string category) => category switch
    {
        TitleCategories.Movie => "movie",
        TitleCategories.Series => "series",
        TitleCategories.StandUp => "standup",
        _ => category.ToLowerInvariant(),
    };

    private static string ToApiMetadataStatus(string status) => status.ToLowerInvariant() switch
    {
        "pending" => "pending",
        "ready" => "ready",
        "unmapped" => "unmapped",
        "failed" => "failed",
        _ => status.ToLowerInvariant(),
    };

}
