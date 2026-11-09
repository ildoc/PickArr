using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Core.Metadata;
using Pickarr.Core.Settings;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Integration.Tests;

[CollectionDefinition("catalog-api")]
public sealed class CatalogApiCollection : ICollectionFixture<ApiFactory>;

[Collection("catalog-api")]
public sealed class CatalogApiTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public CatalogApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Titles_empty_until_Arr_configured_then_filters_by_media_type()
    {
        await ClearArrSettingsAsync();
        var sourceId = await SeedSourceWithTitlesAsync();

        var before = await _client.GetAsync($"/api/v1/catalog/sources/{sourceId}/titles");
        before.StatusCode.ShouldBe(HttpStatusCode.OK);
        var beforeBody = await before.Content.ReadFromJsonAsync<JsonElement>();
        beforeBody.GetProperty("total").GetInt32().ShouldBe(0);
        beforeBody.GetProperty("emptyReason").GetString().ShouldBe("arrNotConfigured");

        var put = await _client.PutAsJsonAsync("/api/v1/settings", new
        {
            sonarr = new { url = "http://sonarr.local", apiKey = "sonarr-key" },
            radarr = new { url = "http://radarr.local", apiKey = "radarr-key" },
        });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        var after = await _client.GetAsync($"/api/v1/catalog/sources/{sourceId}/titles?sort=title&dir=asc");
        after.StatusCode.ShouldBe(HttpStatusCode.OK);
        var afterBody = await after.Content.ReadFromJsonAsync<JsonElement>();
        afterBody.GetProperty("total").GetInt32().ShouldBe(2);
        if (afterBody.TryGetProperty("emptyReason", out var reason))
        {
            reason.ValueKind.ShouldBe(JsonValueKind.Null);
        }

        var firstId = afterBody.GetProperty("items")[0].GetProperty("id").GetInt32();
        var favPut = await _client.PutAsync(
            $"/api/v1/favorites/{firstId}",
            new StringContent(string.Empty));
        favPut.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var catalogWithFavoriteFlag = await _client.GetAsync(
            $"/api/v1/catalog/sources/{sourceId}/titles?favorite=true&sort=title&dir=asc");
        var catalogWithFavoriteBody = await catalogWithFavoriteFlag.Content.ReadFromJsonAsync<JsonElement>();
        catalogWithFavoriteBody.GetProperty("total").GetInt32().ShouldBe(afterBody.GetProperty("total").GetInt32());

        var favList = await _client.GetAsync("/api/v1/favorites");
        favList.StatusCode.ShouldBe(HttpStatusCode.OK);
        var favBody = await favList.Content.ReadFromJsonAsync<JsonElement>();
        favBody.GetProperty("items").EnumerateArray()
            .Any(item => item.GetProperty("id").GetInt32() == firstId)
            .ShouldBeTrue();

        var detail = await _client.GetAsync($"/api/v1/titles/{firstId}?sourceId={sourceId}");
        detail.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detailBody = await detail.Content.ReadFromJsonAsync<JsonElement>();
        detailBody.GetProperty("isFavorite").GetBoolean().ShouldBeTrue();
        detailBody.GetProperty("arr").GetProperty("canAdd").GetBoolean().ShouldBeTrue();

        // Accent-insensitive: ASCII query matches stored "Caffè" via NameNormalized
        var search = await _client.GetAsync($"/api/v1/catalog/sources/{sourceId}/titles?q=caffe");
        var searchBody = await search.Content.ReadFromJsonAsync<JsonElement>();
        searchBody.GetProperty("total").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public async Task Titles_are_filtered_by_the_source_country()
    {
        var put = await _client.PutAsJsonAsync("/api/v1/settings", new
        {
            sonarr = new { url = "http://sonarr.local", apiKey = "sonarr-key" },
            radarr = new { url = "http://radarr.local", apiKey = "radarr-key" },
        });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        int italyId;
        int unitedStatesId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
            var italy = await db.CatalogSources.FirstAsync(s => s.CountryCode == "IT");
            var unitedStates = await db.CatalogSources.FirstAsync(s => s.CountryCode == "US");
            italy.IsEnabled = true;
            unitedStates.IsEnabled = true;
            unitedStates.BaselineCompletedAt ??= DateTime.UtcNow;
            unitedStates.LastFullAt ??= DateTime.UtcNow;
            await db.SaveChangesAsync();
            italyId = italy.Id;
            unitedStatesId = unitedStates.Id;

            if (!await db.Titles.AnyAsync(t => t.OriginalTitle == "Only In America"))
            {
                var now = DateTime.UtcNow;
                var movie = new Title
                {
                    MediaType = MediaTypes.Movie,
                    Category = TitleCategories.Movie,
                    OriginalTitle = "Only In America",
                    OriginalTitleNormalized = "only in america",
                    ReleaseYear = 2024,
                    MetadataStatus = MetadataStatuses.Ready,
                    CreatedAt = now,
                    UpdatedAt = now,
                };
                db.Titles.Add(movie);
                await db.SaveChangesAsync();
                db.Availabilities.Add(new Availability
                {
                    TitleId = movie.Id,
                    CatalogSourceId = unitedStates.Id,
                    FirstSeenAt = now,
                    LastSeenAt = now,
                    AddedAt = now,
                });
                await db.SaveChangesAsync();
            }
        }

        var us = await _client.GetAsync($"/api/v1/catalog/sources/{unitedStatesId}/titles?q=only%20in%20america");
        us.StatusCode.ShouldBe(HttpStatusCode.OK);
        var usBody = await us.Content.ReadFromJsonAsync<JsonElement>();
        usBody.GetProperty("total").GetInt32().ShouldBeGreaterThanOrEqualTo(1);

        var it = await _client.GetAsync($"/api/v1/catalog/sources/{italyId}/titles?q=only%20in%20america");
        var itBody = await it.Content.ReadFromJsonAsync<JsonElement>();
        itBody.GetProperty("total").GetInt32().ShouldBe(0);
    }

    private async Task ClearArrSettingsAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var store = scope.ServiceProvider.GetRequiredService<ISettingsStore>();
        await store.DeleteAsync(SettingsKeys.SonarrUrl);
        await store.DeleteAsync(SettingsKeys.SonarrApiKey);
        await store.DeleteAsync(SettingsKeys.RadarrUrl);
        await store.DeleteAsync(SettingsKeys.RadarrApiKey);
    }

    private async Task<int> SeedSourceWithTitlesAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
        var source = await db.CatalogSources.FirstAsync(s => s.CountryCode == "IT");
        source.IsEnabled = true;
        source.BaselineCompletedAt = DateTime.UtcNow;
        source.LastFullAt = DateTime.UtcNow;

        const string movieTitle = "Caffè Film";
        const string seriesTitle = "Serie Test";
        var now = DateTime.UtcNow;

        if (!await db.Titles.AnyAsync(t => t.OriginalTitle == movieTitle))
        {
            var movie = new Title
            {
                MediaType = MediaTypes.Movie,
                Category = TitleCategories.Movie,
                OriginalTitle = movieTitle,
                OriginalTitleNormalized = TextNormalizer.NormalizeName(movieTitle),
                ReleaseYear = 2024,
                TmdbId = 603,
                TmdbType = "movie",
                MetadataStatus = MetadataStatuses.Ready,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Titles.Add(movie);
            await db.SaveChangesAsync();
            movie.Localizations.Add(new TitleLocalization
            {
                TitleId = movie.Id,
                Language = "it-IT",
                Name = movieTitle,
                NameNormalized = TextNormalizer.NormalizeName(movieTitle),
                FetchedAt = now,
            });
            db.Availabilities.Add(new Availability
            {
                TitleId = movie.Id,
                CatalogSourceId = source.Id,
                FirstSeenAt = now,
                LastSeenAt = now,
                AddedAt = now,
            });
        }

        if (!await db.Titles.AnyAsync(t => t.OriginalTitle == seriesTitle))
        {
            var series = new Title
            {
                MediaType = MediaTypes.Series,
                Category = TitleCategories.Series,
                OriginalTitle = seriesTitle,
                OriginalTitleNormalized = "serie test",
                ReleaseYear = 2023,
                TvdbId = 121361,
                TmdbId = 1396,
                TmdbType = "tv",
                MetadataStatus = MetadataStatuses.Ready,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Titles.Add(series);
            await db.SaveChangesAsync();
            series.Localizations.Add(new TitleLocalization
            {
                TitleId = series.Id,
                Language = "it-IT",
                Name = seriesTitle,
                NameNormalized = "serie test",
                FetchedAt = now,
            });
            db.Availabilities.Add(new Availability
            {
                TitleId = series.Id,
                CatalogSourceId = source.Id,
                FirstSeenAt = now,
                LastSeenAt = now,
                AddedAt = now,
            });
        }

        await db.SaveChangesAsync();

        var italyTitleCount = await db.Availabilities.CountAsync(a =>
            a.CatalogSourceId == source.Id
            && a.RemovedAt == null
            && (a.Title!.OriginalTitle == movieTitle || a.Title.OriginalTitle == seriesTitle));
        italyTitleCount.ShouldBe(2);

        return source.Id;
    }
}
