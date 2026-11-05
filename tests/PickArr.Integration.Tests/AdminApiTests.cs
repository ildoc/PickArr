using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PickArr.Core.Admin;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Infrastructure.Persistence;
using Shouldly;

namespace PickArr.Integration.Tests;

[Collection("api")]
public sealed class AdminApiTests
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public AdminApiTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Quota_returns_watchmode_snapshot()
    {
        var response = await _client.GetAsync("/api/v1/admin/quota");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("service").GetString().ShouldBe("watchmode");
        body.GetProperty("quota").GetInt32().ShouldBeGreaterThan(0);
        body.TryGetProperty("spendable", out _).ShouldBeTrue();
        body.TryGetProperty("projectedMonthEnd", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Jobs_lists_catalog_and_arr_entries()
    {
        await GetEnabledSourceIdAsync();
        var response = await _client.GetAsync("/api/v1/admin/jobs");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.ValueKind.ShouldBe(JsonValueKind.Array);
        body.GetArrayLength().ShouldBeGreaterThan(0);

        var keys = body.EnumerateArray().Select(j => j.GetProperty("jobKey").GetString()).ToList();
        keys.ShouldContain(k => k != null && k.StartsWith("catalog-incr-"));
        keys.ShouldContain("arr-sync-sonarr");
        keys.ShouldContain("metadata-enrichment");
        keys.ShouldContain("catalog-bootstrap");
        keys.ShouldContain("catalog-resume");

        var resume = body.EnumerateArray().First(j => j.GetProperty("jobKey").GetString() == "catalog-resume");
        resume.GetProperty("displayName").GetString().ShouldBe("Resume paused imports");
        resume.GetProperty("description").GetString()!.ShouldContain("does not download titles by itself");
        resume.GetProperty("executionState").GetString().ShouldNotBeNullOrWhiteSpace();

        var bootstrap = body.EnumerateArray().First(j => j.GetProperty("jobKey").GetString() == "catalog-bootstrap");
        bootstrap.GetProperty("displayName").GetString().ShouldBe("Initial catalog import");
    }

    [Fact]
    public async Task Jobs_show_running_state_and_failure_log()
    {
        var sourceId = await GetEnabledSourceIdAsync();
        var failedId = Guid.NewGuid();
        var runningId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
            db.SyncRuns.Add(new SyncRun
            {
                Id = failedId,
                Kind = SyncRunKinds.CatalogFull,
                CatalogSourceId = sourceId,
                TriggeredBy = SyncRunTriggers.Manual,
                StartedAt = DateTime.UtcNow.AddDays(1),
                FinishedAt = DateTime.UtcNow.AddDays(1).AddMinutes(1),
                Status = SyncRunStatuses.Failed,
                ErrorMessage = "integration-failure-marker",
            });
            db.SyncRuns.Add(new SyncRun
            {
                Id = runningId,
                Kind = SyncRunKinds.Leaving,
                CatalogSourceId = sourceId,
                TriggeredBy = SyncRunTriggers.Manual,
                StartedAt = DateTime.UtcNow.AddDays(1),
                Status = SyncRunStatuses.Running,
            });
            await db.SaveChangesAsync();
        }

        try
        {
            var response = await _client.GetAsync("/api/v1/admin/jobs");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            var full = body.EnumerateArray().First(j => j.GetProperty("jobKey").GetString() == $"catalog-full-{sourceId}");
            full.GetProperty("executionState").GetString().ShouldBe(JobExecutionStates.Failed);
            full.GetProperty("lastError").GetString().ShouldBe("integration-failure-marker");

            var leaving = body.EnumerateArray().First(j => j.GetProperty("jobKey").GetString() == $"catalog-leaving-{sourceId}");
            leaving.GetProperty("executionState").GetString().ShouldBe(JobExecutionStates.Running);
        }
        finally
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
            var running = await db.SyncRuns.FirstAsync(r => r.Id == runningId);
            running.Status = SyncRunStatuses.Cancelled;
            running.FinishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }

    [Fact]
    public async Task Runs_lists_seeded_history()
    {
        var runId = await SeedRunAsync();
        var list = await _client.GetAsync("/api/v1/admin/runs?page=1&pageSize=50");
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await list.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("total").GetInt32().ShouldBeGreaterThan(0);

        var detail = await _client.GetAsync($"/api/v1/admin/runs/{runId}");
        detail.StatusCode.ShouldBe(HttpStatusCode.OK);
        var detailBody = await detail.Content.ReadFromJsonAsync<JsonElement>();
        detailBody.GetProperty("id").GetGuid().ShouldBe(runId);
        detailBody.GetProperty("status").GetString().ShouldBe(SyncRunStatuses.Succeeded);
    }

    [Fact]
    public async Task Trigger_rejects_when_already_running()
    {
        var sourceId = await GetEnabledSourceIdAsync();
        await SeedRunningAsync(SyncRunKinds.CatalogIncremental, sourceId);

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/admin/jobs/catalog-incr-{sourceId}/trigger",
            new { sourceId, mode = "incremental" });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("code").GetString().ShouldBe("SYNC_ALREADY_RUNNING");
    }

    private async Task<Guid> SeedRunAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var run = new SyncRun
        {
            Id = Guid.NewGuid(),
            Kind = SyncRunKinds.CatalogIncremental,
            CatalogSourceId = await db.CatalogSources.Select(s => (int?)s.Id).FirstAsync(),
            TriggeredBy = SyncRunTriggers.Manual,
            StartedAt = DateTime.UtcNow.AddMinutes(-5),
            FinishedAt = DateTime.UtcNow.AddMinutes(-4),
            Status = SyncRunStatuses.Succeeded,
            CreditsUsed = 3,
            PagesFetched = 3,
            TitlesAdded = 1,
            TitlesUpdated = 2,
        };
        db.SyncRuns.Add(run);
        await db.SaveChangesAsync();
        return run.Id;
    }

    private async Task SeedRunningAsync(string kind, int sourceId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        db.SyncRuns.Add(new SyncRun
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            CatalogSourceId = sourceId,
            TriggeredBy = SyncRunTriggers.Manual,
            StartedAt = DateTime.UtcNow,
            Status = SyncRunStatuses.Running,
        });
        await db.SaveChangesAsync();
    }

    private async Task<int> GetEnabledSourceIdAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PickArrDbContext>();
        var source = await db.CatalogSources.FirstOrDefaultAsync(s => s.IsEnabled)
            ?? await db.CatalogSources.FirstAsync();
        if (!source.IsEnabled)
        {
            source.IsEnabled = true;
            await db.SaveChangesAsync();
        }

        return source.Id;
    }
}
