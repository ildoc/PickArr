using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PickArr.Core.Abstractions;
using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;
using PickArr.Core.Metadata;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Catalog;
using PickArr.Infrastructure.Persistence;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

public sealed class RecoEventLogServiceTests : IAsyncLifetime
{
    private SqliteConnection? _connection;

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Log_is_noop_when_events_disabled_by_default()
    {
        var (db, log, titleId) = await SeedAsync(eventsEnabled: false);
        await log.LogAsync(titleId, RecoEventKinds.Shown, version: 1, rank: 3, scope: "source");
        (await db.RecoEventLogs.CountAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Log_persists_when_enabled()
    {
        var (db, log, titleId) = await SeedAsync(eventsEnabled: true);
        await log.LogManyAsync(
        [
            new RecoEventLogEntry(titleId, RecoEventKinds.Shown, 2, 1, "enabled"),
            new RecoEventLogEntry(titleId, RecoEventKinds.Opened, 2, 1, "enabled"),
            new RecoEventLogEntry(titleId, RecoEventKinds.Liked, 2, 1, "enabled"),
        ]);

        var rows = await db.RecoEventLogs.OrderBy(e => e.Id).ToListAsync();
        rows.Count.ShouldBe(3);
        rows.Select(r => r.Event).ShouldBe([RecoEventKinds.Shown, RecoEventKinds.Opened, RecoEventKinds.Liked]);
    }

    private async Task<(PickArrDbContext Db, RecoEventLogService Log, int TitleId)> SeedAsync(bool eventsEnabled)
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        await _connection.OpenAsync();
        var options = new DbContextOptionsBuilder<PickArrDbContext>().UseSqlite(_connection).Options;
        var db = new PickArrDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        var title = new Title
        {
            MediaType = MediaTypes.Movie,
            Category = TitleCategories.Movie,
            OriginalTitle = "Event Movie",
            MetadataStatus = MetadataStatuses.Ready,
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.Titles.Add(title);
        await db.SaveChangesAsync();

        var settings = new MemorySettingsStore();
        await settings.SetAsync(
            SettingsKeys.RecoEventsEnabled,
            eventsEnabled ? "true" : "false",
            false);

        var log = new RecoEventLogService(db, new FixedClock(now), settings);
        return (db, log, title.Id);
    }

    private sealed class FixedClock(DateTime utcNow) : IClock
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class MemorySettingsStore : ISettingsStore
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public Task<string?> GetRawAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out var v) ? v : null);

        public Task SetAsync(string key, string valueJson, bool isSecret, CancellationToken cancellationToken = default)
        {
            _values[key] = valueJson;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
        {
            _values.Remove(key);
            return Task.CompletedTask;
        }

        public Task<bool> IsSetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.ContainsKey(key));
    }
}
