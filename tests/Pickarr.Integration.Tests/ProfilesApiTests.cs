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

[CollectionDefinition("profiles-api")]
public sealed class ProfilesApiCollection : ICollectionFixture<ApiFactory>;

[Collection("profiles-api")]
public sealed class ProfilesApiTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _admin;
    private readonly HttpClient _plain;

    public ProfilesApiTests(ApiFactory factory)
    {
        _factory = factory;
        _admin = factory.WithAdmin();
        _plain = factory.CreateClient();
    }

    [Fact]
    public async Task Get_lists_profiles_without_a_header()
    {
        var response = await _plain.GetAsync("/api/v1/profiles");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateArray().ShouldContain(profile =>
            profile.GetProperty("isAdmin").GetBoolean()
            && profile.GetProperty("name").GetString() == Profile.AdminName);
    }

    [Fact]
    public async Task Create_and_edit_follow_the_declared_role()
    {
        var anonymous = await _plain.PostAsJsonAsync("/api/v1/profiles", new { name = "Anon" });
        anonymous.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var created = await _admin.PostAsJsonAsync("/api/v1/profiles", new { name = "  Kid  ", color = "RED" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var kid = await created.Content.ReadFromJsonAsync<JsonElement>();
        kid.GetProperty("name").GetString().ShouldBe("Kid");
        kid.GetProperty("color").GetString().ShouldBe("red");
        kid.GetProperty("isAdmin").GetBoolean().ShouldBeFalse();
        kid.GetProperty("useImplicit").GetBoolean().ShouldBeTrue();
        var kidId = kid.GetProperty("id").GetInt32();

        var kidClient = _factory.CreateClient();
        kidClient.UseProfile(kidId);

        var asKid = await kidClient.PostAsJsonAsync("/api/v1/profiles", new { name = "Sibling" });
        asKid.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var self = await kidClient.PatchAsJsonAsync($"/api/v1/profiles/{kidId}", new
        {
            name = "Child",
            color = "teal",
            useImplicit = false,
            excludeInLibrary = false,
        });
        self.StatusCode.ShouldBe(HttpStatusCode.OK);
        var selfBody = await self.Content.ReadFromJsonAsync<JsonElement>();
        selfBody.GetProperty("name").GetString().ShouldBe("Child");
        selfBody.GetProperty("useImplicit").GetBoolean().ShouldBeFalse();
        selfBody.GetProperty("excludeInLibrary").GetBoolean().ShouldBeFalse();
        selfBody.GetProperty("isAdmin").GetBoolean().ShouldBeFalse();

        var role = await kidClient.PatchAsJsonAsync($"/api/v1/profiles/{kidId}", new { isAdmin = true });
        role.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var adminId = _factory.AdminProfileId();
        var other = await kidClient.PatchAsJsonAsync($"/api/v1/profiles/{adminId}", new { name = "Taken" });
        other.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var byAdmin = await _admin.PatchAsJsonAsync($"/api/v1/profiles/{kidId}", new { color = "purple" });
        byAdmin.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await byAdmin.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("color").GetString().ShouldBe("purple");
    }

    [Fact]
    public async Task Invalid_name_or_color_is_rejected()
    {
        var empty = await _admin.PostAsJsonAsync("/api/v1/profiles", new { name = "   " });
        empty.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var longName = await _admin.PostAsJsonAsync("/api/v1/profiles", new { name = new string('a', 41) });
        longName.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var color = await _admin.PostAsJsonAsync("/api/v1/profiles", new { name = "Goldie", color = "gold" });
        color.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var duplicate = await _admin.PostAsJsonAsync("/api/v1/profiles", new { name = "admin" });
        duplicate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Delete_removes_personal_rows_and_keeps_the_audit()
    {
        var created = await _admin.PostAsJsonAsync("/api/v1/profiles", new { name = "Leaver" });
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        var profileId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        var adminId = _factory.AdminProfileId();
        var titleId = await SeedPersonalRowsAsync(profileId, adminId);

        var asProfile = _factory.CreateClient();
        asProfile.UseProfile(profileId);
        var forbidden = await asProfile.DeleteAsync($"/api/v1/profiles/{profileId}");
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var adminDelete = await _admin.DeleteAsync($"/api/v1/profiles/{adminId}");
        adminDelete.StatusCode.ShouldBe(HttpStatusCode.Conflict);

        var deleted = await _admin.DeleteAsync($"/api/v1/profiles/{profileId}");
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
        (await db.Profiles.AnyAsync(profile => profile.Id == profileId)).ShouldBeFalse();
        (await db.Favorites.AnyAsync(row => row.ProfileId == profileId)).ShouldBeFalse();
        (await db.Ratings.AnyAsync(row => row.ProfileId == profileId)).ShouldBeFalse();
        (await db.RatingEvents.AnyAsync(row => row.ProfileId == profileId)).ShouldBeFalse();
        (await db.Watched.AnyAsync(row => row.ProfileId == profileId)).ShouldBeFalse();
        (await db.Recommendations.AnyAsync(row => row.ProfileId == profileId)).ShouldBeFalse();
        (await db.RecoStates.AnyAsync(row => row.ProfileId == profileId)).ShouldBeFalse();
        (await db.RecoEventLogs.AnyAsync(row => row.ProfileId == profileId)).ShouldBeFalse();
        (await db.Favorites.AnyAsync(row => row.ProfileId == adminId && row.TitleId == titleId)).ShouldBeTrue();
        var audit = await db.ArrAuditLogs.SingleAsync(row => row.TitleId == titleId);
        audit.ProfileId.ShouldBeNull();
        (await db.Titles.AnyAsync(title => title.Id == titleId)).ShouldBeTrue();
    }

    private async Task<int> SeedPersonalRowsAsync(int profileId, int adminId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
        var now = DateTime.UtcNow;
        var title = new Title
        {
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = "Profile Delete",
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Titles.Add(title);
        await db.SaveChangesAsync();

        db.Favorites.Add(new Favorite { ProfileId = profileId, TitleId = title.Id, CreatedAt = now });
        db.Favorites.Add(new Favorite { ProfileId = adminId, TitleId = title.Id, CreatedAt = now });
        db.Ratings.Add(new Rating { ProfileId = profileId, TitleId = title.Id, Value = RatingValues.Like, RatedAt = now });
        db.RatingEvents.Add(new RatingEvent
        {
            ProfileId = profileId,
            TitleId = title.Id,
            Value = RatingValues.Like,
            At = now,
            Origin = RatingEventOrigins.Ui,
        });
        db.Watched.Add(new Watched { ProfileId = profileId, TitleId = title.Id, WatchedAt = now });
        db.Recommendations.Add(new Recommendation
        {
            Version = 9,
            TitleId = title.Id,
            ProfileId = profileId,
            Score = 1,
            Prior = 1,
            Rank = 1,
            ReasonsJson = "[]",
        });
        db.RecoStates.Add(new RecoState { ProfileId = profileId, Stage = "cold", ModelStatus = "Absent" });
        db.RecoEventLogs.Add(new RecoEventLog
        {
            ProfileId = profileId,
            TitleId = title.Id,
            At = now,
            Event = "impression",
        });
        db.ArrAuditLogs.Add(new ArrAuditLog
        {
            ProfileId = profileId,
            At = now,
            Action = ArrAuditActions.Add,
            Kind = "radarr",
            TitleId = title.Id,
            Options = "{}",
            Outcome = ArrAuditOutcomes.Success,
        });
        await db.SaveChangesAsync();
        return title.Id;
    }
}

[CollectionDefinition("profiles-limit-api")]
public sealed class ProfilesLimitApiCollection : ICollectionFixture<ApiFactory>;

[Collection("profiles-limit-api")]
public sealed class ProfilesLimitApiTests
{
    private readonly HttpClient _admin;

    public ProfilesLimitApiTests(ApiFactory factory)
    {
        _admin = factory.WithAdmin();
    }

    [Fact]
    public async Task The_eleventh_profile_is_rejected()
    {
        for (var i = 1; i < Profile.MaxProfiles; i++)
        {
            var created = await _admin.PostAsJsonAsync("/api/v1/profiles", new { name = $"Seat {i}" });
            created.StatusCode.ShouldBe(HttpStatusCode.Created);
        }

        var overflow = await _admin.PostAsJsonAsync("/api/v1/profiles", new { name = "Overflow" });
        overflow.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
