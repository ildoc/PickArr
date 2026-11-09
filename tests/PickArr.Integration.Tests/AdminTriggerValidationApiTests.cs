using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Pickarr.Integration.Tests;

[CollectionDefinition("admin-trigger-validation")]
public sealed class AdminTriggerValidationApiCollection : ICollectionFixture<ApiFactory>;

[Collection("admin-trigger-validation")]
public sealed class AdminTriggerValidationApiTests
{
    private readonly HttpClient _client;

    public AdminTriggerValidationApiTests(ApiFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Trigger_unknown_job_returns_invalid_trigger()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/jobs/not-a-real-job/trigger",
            new { });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("INVALID_TRIGGER");
        body.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("Unknown jobKey");
    }

    [Fact]
    public async Task Trigger_catalog_without_source_returns_invalid_trigger()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/admin/jobs/catalog/trigger",
            new { mode = "incremental" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("INVALID_TRIGGER");
        body.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("sourceId");
    }
}
