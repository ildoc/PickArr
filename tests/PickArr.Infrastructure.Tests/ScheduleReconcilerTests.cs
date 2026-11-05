using Hangfire;
using Hangfire.InMemory;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain.Entities;
using PickArr.Infrastructure.Jobs;
using PickArr.Infrastructure.Persistence;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

public sealed class ScheduleReconcilerTests
{
    private sealed class EmptySettingsStore : ISettingsStore
    {
        public Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SetAsync(string key, string valueJson, bool isSecret, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    [Fact]
    public async Task ReconcileAsync_registers_jobs_for_enabled_sources_only()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickArrDbContext>().UseSqlite(connection).Options;
        await using var db = new PickArrDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var provider = new Provider
        {
            Key = "netflix",
            DisplayName = "Netflix",
            ProviderType = "watchmode",
            ExternalId = "203",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
        };
        db.Providers.Add(provider);
        await db.SaveChangesAsync();

        db.CatalogSources.Add(new CatalogSource
        {
            ProviderId = provider.Id,
            CountryCode = "US",
            MetadataLanguage = "en-US",
            IsEnabled = true,
            IncrementalCron = "0 4 * * *",
            FullSyncCron = "0 3 * * 0",
        });
        db.CatalogSources.Add(new CatalogSource
        {
            ProviderId = provider.Id,
            CountryCode = "IT",
            MetadataLanguage = "it-IT",
            IsEnabled = false,
        });
        await db.SaveChangesAsync();

        var storage = new InMemoryStorage();
        JobStorage.Current = storage;
        var manager = new RecurringJobManager(storage);

        var usId = db.CatalogSources.Single(s => s.CountryCode == "US").Id;
        var itId = db.CatalogSources.Single(s => s.CountryCode == "IT").Id;

        var reconciler = new ScheduleReconciler(
            db,
            manager,
            new EmptySettingsStore(),
            NullLogger<ScheduleReconciler>.Instance);
        await reconciler.ReconcileAsync();

        using var hangfireConnection = storage.GetConnection();
        var recurring = hangfireConnection.GetAllItemsFromSet("recurring-jobs");
        recurring.ShouldContain(ScheduleReconciler.ResumeJobId);
        recurring.ShouldContain(ScheduleReconciler.IncrementalJobId(usId));
        recurring.ShouldContain(ScheduleReconciler.FullJobId(usId));
        recurring.ShouldContain(LeavingSyncJob.JobId(usId));
        recurring.ShouldContain(ArrSyncJob.SonarrJobId);
        recurring.ShouldContain(ArrSyncJob.RadarrJobId);
        recurring.ShouldNotContain(ScheduleReconciler.IncrementalJobId(itId));
        recurring.ShouldNotContain(ScheduleReconciler.FullJobId(itId));
        recurring.ShouldNotContain(LeavingSyncJob.JobId(itId));
    }
}
