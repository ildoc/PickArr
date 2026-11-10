using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Metadata;
using Pickarr.Infrastructure.Metadata;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class SeriesGuideTests
{
    [Fact]
    public async Task Seasons_are_newest_first_and_episodes_are_cached_in_order()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickarrDbContext>().UseSqlite(connection).Options;
        await using var db = new PickarrDbContext(options);
        await db.Database.EnsureCreatedAsync();

        db.Titles.Add(new Title
        {
            MediaType = MediaTypes.Series,
            Category = TitleCategories.Series,
            OriginalTitle = "Example",
            TmdbId = 42,
            TmdbType = "tv",
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var titleId = db.Titles.Single().Id;

        var tmdb = new ScriptedTmdb();
        var guide = new SeriesGuideService(db, tmdb);

        var seasons = await guide.ListSeasonsAsync(titleId, sourceId: null);
        seasons.ShouldNotBeNull();
        seasons.Select(season => season.Number).ShouldBe([2, 1]);

        var episodes = await guide.ListEpisodesAsync(titleId, seasonNumber: 2, sourceId: null);
        episodes.ShouldNotBeNull();
        episodes.Select(episode => episode.Number).ShouldBe([1, 3]);
        tmdb.SeasonCalls.ShouldBe(1);

        var again = await guide.ListEpisodesAsync(titleId, seasonNumber: 2, sourceId: null);
        again!.Select(episode => episode.Number).ShouldBe([1, 3]);
        tmdb.SeasonCalls.ShouldBe(1);
    }

    [Fact]
    public async Task Missing_title_returns_null_for_seasons_and_episodes()
    {
        await using var db = await CreateDbAsync();
        var guide = new SeriesGuideService(db, new ScriptedTmdb());

        (await guide.ListSeasonsAsync(titleId: 404, sourceId: null)).ShouldBeNull();
        (await guide.ListEpisodesAsync(titleId: 404, seasonNumber: 1, sourceId: null)).ShouldBeNull();
    }

    [Fact]
    public async Task Movie_or_series_without_tmdb_returns_empty_lists()
    {
        await using var db = await CreateDbAsync();
        db.Titles.AddRange(
            new Title
            {
                MediaType = MediaTypes.Movie,
                Category = TitleCategories.Movie,
                OriginalTitle = "A Film",
                TmdbId = 99,
                TmdbType = "movie",
                MetadataStatus = MetadataStatuses.Ready,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            },
            new Title
            {
                MediaType = MediaTypes.Series,
                Category = TitleCategories.Series,
                OriginalTitle = "No Tmdb",
                TmdbId = null,
                MetadataStatus = MetadataStatuses.Ready,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();
        var movieId = db.Titles.Single(t => t.OriginalTitle == "A Film").Id;
        var seriesId = db.Titles.Single(t => t.OriginalTitle == "No Tmdb").Id;
        var tmdb = new ScriptedTmdb();
        var guide = new SeriesGuideService(db, tmdb);

        (await guide.ListSeasonsAsync(movieId, null)).ShouldBeEmpty();
        (await guide.ListEpisodesAsync(movieId, 1, null)).ShouldBeEmpty();
        (await guide.ListSeasonsAsync(seriesId, null)).ShouldBeEmpty();
        (await guide.ListEpisodesAsync(seriesId, 1, null)).ShouldBeEmpty();
        tmdb.TvCalls.ShouldBe(0);
        tmdb.SeasonCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Seasons_stay_empty_when_tmdb_returns_null_details()
    {
        await using var db = await CreateDbAsync();
        db.Titles.Add(new Title
        {
            MediaType = MediaTypes.Series,
            Category = TitleCategories.Series,
            OriginalTitle = "Ghost",
            TmdbId = 7,
            TmdbType = "tv",
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        var titleId = db.Titles.Single().Id;
        var tmdb = new ScriptedTmdb { ReturnNullDetails = true };
        var guide = new SeriesGuideService(db, tmdb);

        var seasons = await guide.ListSeasonsAsync(titleId, null);
        seasons.ShouldNotBeNull();
        seasons.ShouldBeEmpty();
        tmdb.TvCalls.ShouldBe(1);
        (await db.Titles.SingleAsync()).SeasonsJson.ShouldBeNull();
    }

    private static async Task<PickarrDbContext> CreateDbAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickarrDbContext>().UseSqlite(connection).Options;
        var db = new PickarrDbContext(options);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private sealed class ScriptedTmdb : IMetadataProvider
    {
        public int SeasonCalls { get; private set; }
        public int TvCalls { get; private set; }
        public bool ReturnNullDetails { get; init; }
        public string Type => "tmdb";

        public Task<TmdbTitleDetails?> GetTvAsync(int tmdbId, string language, CancellationToken cancellationToken = default)
        {
            TvCalls++;
            if (ReturnNullDetails)
            {
                return Task.FromResult<TmdbTitleDetails?>(null);
            }

            return Task.FromResult<TmdbTitleDetails?>(new TmdbTitleDetails(
                tmdbId, "Example", "Example", null, null, 2024, null, 2, 4,
                null, null, null, "en", null, null, null, null,
                [], [], false, null,
                [
                    new TmdbSeason(1, "Season 1", 2, "2020-01-01", null),
                    new TmdbSeason(2, "Season 2", 2, "2024-01-01", null),
                ]));
        }

        public Task<IReadOnlyList<TmdbEpisode>> GetTvSeasonAsync(
            int tmdbId, int seasonNumber, string language, CancellationToken cancellationToken = default)
        {
            SeasonCalls++;
            IReadOnlyList<TmdbEpisode> episodes =
            [
                new TmdbEpisode(3, "Later", null, null, null),
                new TmdbEpisode(1, "Earlier", null, null, null),
            ];
            return Task.FromResult(episodes);
        }

        public Task<TmdbTitleDetails?> GetMovieAsync(int tmdbId, string language, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TmdbRecommendationRef>> GetRecommendationsAsync(
            int tmdbId,
            bool isMovie,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<TmdbRecommendationRef>>([]);

        public Task<TmdbFindResult?> FindByImdbAsync(string imdbId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TmdbGenre>> GetMovieGenresAsync(string language, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<TmdbGenre>> GetTvGenresAsync(string language, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<TmdbImageConfig> GetImageConfigAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
