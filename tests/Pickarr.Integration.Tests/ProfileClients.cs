using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pickarr.Core.Profiles;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Integration.Tests;

internal static class ProfileClients
{
    public static HttpClient WithAdmin(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        client.UseProfile(factory.AdminProfileId());
        return client;
    }

    public static int AdminProfileId(this ApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickarrDbContext>();
        return db.Profiles.Where(profile => profile.IsAdmin).Select(profile => profile.Id).Single();
    }

    public static void UseProfile(this HttpClient client, int profileId)
    {
        client.DefaultRequestHeaders.Remove(ProfileHeader.Name);
        client.DefaultRequestHeaders.TryAddWithoutValidation(ProfileHeader.Name, profileId.ToString());
    }
}
