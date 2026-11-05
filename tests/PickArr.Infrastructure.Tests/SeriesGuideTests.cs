using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Metadata;
using PickArr.Infrastructure.Metadata;
using PickArr.Infrastructure.Persistence;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

public sealed class SeriesGuideTests
{
    [Fact]
    public async Task Seasons_are_newest_first_and_episodes_are_cached_in_order()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickArrDbContext>().UseSqlite(connection).Options;
        await using var db = new PickArrDbContext(options);
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

    private sealed class ScriptedTmdb : IMetadataProvider
    {
        public int SeasonCalls { get; private set; }
        public string Type => "tmdb";

        public Task<TmdbTitleDetails?> GetTvAsync(int tmdbId, string language, CancellationToken cancellationToken = default) =>
            Task.FromResult<TmdbTitleDetails?>(new TmdbTitleDetails(
                tmdbId, "Example", "Example", null, null, 2024, null, 2, 4,
                null, null, null, "en", null, null, null, null,
                [], [], false, null,
                [
                    new TmdbSeason(1, "Season 1", 2, "2020-01-01", null),
                    new TmdbSeason(2, "Season 2", 2, "2024-01-01", null),
                ]));

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
