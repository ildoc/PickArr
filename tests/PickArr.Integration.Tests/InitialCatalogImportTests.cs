using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace PickArr.Integration.Tests;

public sealed class InitialCatalogImportTests : IAsyncLifetime
{
    private readonly WireMockServer _server = WireMockServer.Start();
    private InitialImportApiFactory? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        var body = await File.ReadAllTextAsync(FindFixture("list-titles-page1.json"));
        _server
            .Given(Request.Create().WithPath("/v1/list-titles/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("X-Account-Quota", "2500")
                .WithHeader("X-Account-Quota-Used", "1")
                .WithBody(body));

        _factory = new InitialImportApiFactory(_server.Urls[0] + "/v1/");
        _client = _factory.CreateClient();
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
        _server.Stop();
        _server.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Saving_watchmode_key_imports_the_first_catalog_pages()
    {
        var client = _client ?? throw new InvalidOperationException("Client was not created.");
        var put = await client.PutAsJsonAsync("/api/v1/settings", new
        {
            watchmode = new { apiKey = "wm-initial-import" },
            sonarr = new { url = "http://sonarr.local", apiKey = "sonarr-key" },
            radarr = new { url = "http://radarr.local", apiKey = "radarr-key" },
        });
        put.StatusCode.ShouldBe(HttpStatusCode.OK);

        JsonElement sources = default;
        for (var attempt = 0; attempt < 20; attempt++)
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
        for (var attempt = 0; attempt < 40; attempt++)
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

    private sealed class InitialImportApiFactory(string watchmodeBaseUrl) : WebApplicationFactory<Program>
    {
        private readonly string _dataPath = Path.Combine(Path.GetTempPath(), "PickArr-import-tests", Guid.NewGuid().ToString("N"));

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            Directory.CreateDirectory(_dataPath);
            builder.UseEnvironment("Development");
            builder.UseSetting("PickArr:DataPath", _dataPath);
            builder.UseSetting("Hangfire:Storage", "Memory");
            builder.UseSetting("PickArr:Catalog:AutoInitialImport", "true");
            builder.UseSetting("PickArr:Watchmode:BaseUrl", watchmodeBaseUrl);
            builder.UseSetting("PickArr:Watchmode:RequestsPerSecond", "1000");
            builder.UseSetting("PickArr:Watchmode:DisableResilience", "true");
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(_dataPath))
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
