using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pickarr.Core.Domain;
using Pickarr.Core.Domain.Entities;
using Pickarr.Infrastructure.Persistence;
using Shouldly;

namespace Pickarr.Integration.Tests;

[CollectionDefinition("personal-header-api")]
public sealed class PersonalHeaderApiCollection : ICollectionFixture<ApiFactory>;

[Collection("personal-header-api")]
public sealed class PersonalHeaderApiTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _admin;
    private readonly HttpClient _plain;

    public PersonalHeaderApiTests(ApiFactory factory)
    {
        _factory = factory;
        _admin = factory.WithAdmin();
        _plain = factory.CreateClient();
    }

    [Fact]
    public async Task Personal_writes_and_lists_stay_on_the_declared_profile()
    {
        var titleId = await SeedTitleAsync();
        var configured = await _plain.PutAsJsonAsync("/api/v1/settings", new
        {
            sonarr = new { url = "http://sonarr.local", apiKey = "sonarr-key" },
            radarr = new { url = "http://radarr.local", apiKey = "radarr-key" },
        });
        configured.StatusCode.ShouldBe(HttpStatusCode.OK);
        var otherId = await CreateProfileAsync("Other");
        var adminId = _factory.AdminProfileId();

        var favorite = await _plain.PutAsync($"/api/v1/favorites/{titleId}", null);
        favorite.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var rating = await _plain.PutAsJsonAsync($"/api/v1/titles/{titleId}/rating", new { value = "like" });
        rating.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var watched = await _plain.PutAsync($"/api/v1/titles/{titleId}/watched", null);
        watched.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var recommendations = await _plain.GetAsync("/api/v1/recommendations?scope=enabled");
        recommendations.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var favoritesList = await _plain.GetAsync("/api/v1/favorites");
        favoritesList.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var settings = await _plain.GetAsync("/api/v1/settings");
        settings.StatusCode.ShouldBe(HttpStatusCode.OK);

        var added = await _admin.PutAsync($"/api/v1/favorites/{titleId}", null);
        added.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var rated = await _admin.PutAsJsonAsync($"/api/v1/titles/{titleId}/rating", new { value = "like" });
        rated.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var seen = await _admin.PutAsync($"/api/v1/titles/{titleId}/watched", null);
        seen.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var other = _factory.CreateClient();
        other.UseProfile(otherId);
        var otherList = await other.GetAsync($"/api/v1/favorites?q={Uri.EscapeDataString("Header Title")}");
        otherList.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await otherList.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32().ShouldBe(0);
        var otherRatings = await other.GetAsync("/api/v1/ratings?value=like");
        (await otherRatings.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32().ShouldBe(0);
        var otherRemove = await other.DeleteAsync($"/api/v1/favorites/{titleId}");
        otherRemove.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var adminList = await _admin.GetAsync($"/api/v1/favorites?q={Uri.EscapeDataString("Header Title")}");
        (await adminList.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("total").GetInt32().ShouldBe(1);
        var adminRatings = await _admin.GetAsync("/api/v1/ratings?value=like");
        var ratingItems = (await adminRatings.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray();
        ratingItems.ShouldContain(item => item.GetProperty("id").GetInt32() == titleId);

        var reco = await _admin.GetAsync("/api/v1/recommendations?scope=enabled");
        reco.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
        (await db.Favorites.AnyAsync(row => row.TitleId == titleId && row.ProfileId == adminId)).ShouldBeTrue();
        (await db.Favorites.AnyAsync(row => row.TitleId == titleId && row.ProfileId == otherId)).ShouldBeFalse();
        (await db.Ratings.AnyAsync(row => row.TitleId == titleId && row.ProfileId == adminId)).ShouldBeTrue();
        (await db.Watched.AnyAsync(row => row.TitleId == titleId && row.ProfileId == adminId)).ShouldBeTrue();
        (await db.Watched.AnyAsync(row => row.TitleId == titleId && row.ProfileId == otherId)).ShouldBeFalse();
    }

    private async Task<int> CreateProfileAsync(string name)
    {
        var created = await _admin.PostAsJsonAsync("/api/v1/profiles", new { name });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
    }

    private async Task<int> SeedTitleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
        var now = DateTime.UtcNow;
        var title = new Title
        {
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = "Header Title",
            OriginalTitleNormalized = "header title",
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Titles.Add(title);
        await db.SaveChangesAsync();
        return title.Id;
    }
}
