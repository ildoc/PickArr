using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace PickArr.Integration.Tests;

[CollectionDefinition("env-initial-import", DisableParallelization = true)]
public sealed class EnvInitialImportCollection;

/// <summary>
/// Cold-start import when the Watchmode key is only declared via configuration.
/// Kept out of <see cref="InitialCatalogImportTests"/> so no other host warms Hangfire first.
/// </summary>
[Collection("env-initial-import")]
public sealed class EnvInitialCatalogImportTests
{
    [Fact]
    public async Task Watchmode_key_from_configuration_imports_at_startup()
    {
        var body = await File.ReadAllTextAsync(FindFixture("list-titles-page1.json"));
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath("/v1/list-titles/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("X-Account-Quota", "2500")
                .WithHeader("X-Account-Quota-Used", "1")
                .WithBody(body));

        await using var factory = new Factory(server.Urls[0] + "/v1/");
        var client = factory.CreateClient();

        JsonElement sources = default;
        for (var attempt = 0; attempt < 40; attempt++)
        {
            var response = await client.GetAsync("/api/v1/catalog/sources");
            sources = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
            if (sources.GetArrayLength() >= 2)
            {
                break;
            }

            await Task.Delay(100);
        }

        sources.GetArrayLength().ShouldBe(2);
        var italy = sources.EnumerateArray().First(s => s.GetProperty("countryCode").GetString() == "IT");
        var sourceId = italy.GetProperty("id").GetInt32();

        JsonElement titles = default;
        for (var attempt = 0; attempt < 60; attempt++)
        {
            var response = await client.GetAsync($"/api/v1/catalog/sources/{sourceId}/titles");
            titles = (await response.Content.ReadFromJsonAsync<JsonElement>())!;
            if (titles.GetProperty("total").GetInt32() > 0)
            {
                break;
            }

            await Task.Delay(250);
        }

        titles.GetProperty("total").GetInt32().ShouldBeGreaterThan(0);

        var jobs = await client.GetAsync("/api/v1/admin/jobs");
        var jobsBody = (await jobs.Content.ReadFromJsonAsync<JsonElement>())!;
        jobsBody.EnumerateArray().ShouldContain(job =>
            job.GetProperty("jobKey").GetString() == "catalog-bootstrap"
            && job.GetProperty("displayName").GetString() == "Initial catalog import");
    }

    private static string FindFixture(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "fixtures", "watchmode", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            candidate = Path.Combine(dir.FullName, "fixtures", "watchmode", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new FileNotFoundException(fileName);
    }

    private sealed class Factory(string watchmodeBaseUrl) : WebApplicationFactory<Program>
    {
        private readonly string _dataPath = Path.Combine(Path.GetTempPath(), "PickArr-env-import-cold", Guid.NewGuid().ToString("N"));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(_dataPath);
            builder.UseEnvironment("Development");
            builder.UseSetting("PickArr:DataPath", _dataPath);
            builder.UseSetting("Hangfire:Storage", "Memory");
            builder.UseSetting("PickArr:Catalog:AutoInitialImport", "true");
            builder.UseSetting("PickArr:Settings:watchmode:apiKey", "wm-env-initial-import");
            builder.UseSetting("PickArr:Settings:sonarr:url", "http://sonarr.local");
            builder.UseSetting("PickArr:Settings:sonarr:apiKey", "sonarr-key");
            builder.UseSetting("PickArr:Settings:radarr:url", "http://radarr.local");
            builder.UseSetting("PickArr:Settings:radarr:apiKey", "radarr-key");
            builder.UseSetting("PickArr:Watchmode:BaseUrl", watchmodeBaseUrl);
            builder.UseSetting("PickArr:Watchmode:RequestsPerSecond", "1000");
            builder.UseSetting("PickArr:Watchmode:DisableResilience", "true");
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            if (Directory.Exists(_dataPath))
            {
                try
                {
                    Directory.Delete(_dataPath, recursive: true);
                }
                catch
                {
                    // best-effort cleanup
                }
            }
        }
    }
}
