using Hangfire;
using Hangfire.Common;
using Hangfire.Storage.SQLite;
using Microsoft.Extensions.Logging.Abstractions;
using Pickarr.Infrastructure.Jobs;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class HangfireSqliteStorageTests
{
    [Fact]
    public void Sqlite_storage_survives_restart_and_keeps_recurring_job()
    {
        var dataPath = Path.Combine(Path.GetTempPath(), "Pickarr-hf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataPath);
        var sqlitePath = Path.Combine(dataPath, "jobs.db");

        try
        {
            using (var storage1 = CreateStorage(sqlitePath))
            {
                var manager = new RecurringJobManager(storage1);
                manager.AddOrUpdate(
                    ScheduleReconcilerStub.ProbeJobId,
                    Job.FromExpression(() => SpikeProbeStatic.Execute()),
                    "0 5 * * *");

                RecurringJobIds(storage1).ShouldContain(ScheduleReconcilerStub.ProbeJobId);
            }

            File.Exists(sqlitePath).ShouldBeTrue();

            using var storage2 = CreateStorage(sqlitePath);
            RecurringJobIds(storage2).ShouldContain(
                ScheduleReconcilerStub.ProbeJobId,
                customMessage: "SQLite Hangfire storage must retain recurring jobs across restarts");
        }
        finally
        {
            TryDeleteDirectory(dataPath);
        }
    }

    [Fact]
    public void ScheduleReconciler_reapplies_probe_after_storage_reset()
    {
        var dataPath = Path.Combine(Path.GetTempPath(), "Pickarr-hf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataPath);
        var sqlitePath = Path.Combine(dataPath, "jobs.db");

        try
        {
            using (var storage1 = CreateStorage(sqlitePath))
            {
                var manager = new RecurringJobManager(storage1);
                var reconciler = new ScheduleReconcilerStub(manager, NullLogger<ScheduleReconcilerStub>.Instance);
                reconciler.ReconcileProbe("0 6 * * *");
                RecurringJobIds(storage1).ShouldContain(ScheduleReconcilerStub.ProbeJobId);
            }

            File.Delete(sqlitePath);
            File.Exists(sqlitePath).ShouldBeFalse();

            using var storage2 = CreateStorage(sqlitePath);
            var manager2 = new RecurringJobManager(storage2);
            var reconciler2 = new ScheduleReconcilerStub(manager2, NullLogger<ScheduleReconcilerStub>.Instance);
            reconciler2.ReconcileProbe("0 6 * * *");

            RecurringJobIds(storage2).ShouldContain(ScheduleReconcilerStub.ProbeJobId);

            using var connection = storage2.GetConnection();
            var entries = connection.GetAllEntriesFromHash($"recurring-job:{ScheduleReconcilerStub.ProbeJobId}");
            entries.ShouldNotBeNull();
            entries["Cron"].ShouldBe("0 6 * * *");
        }
        finally
        {
            TryDeleteDirectory(dataPath);
        }
    }

    private static IReadOnlyCollection<string> RecurringJobIds(SQLiteStorage storage)
    {
        using var connection = storage.GetConnection();
        return connection.GetAllItemsFromSet("recurring-jobs");
    }

    private static SQLiteStorage CreateStorage(string sqlitePath)
        => new(sqlitePath, new SQLiteStorageOptions
        {
            JournalMode = SQLiteStorageOptions.JournalModes.WAL,
        });

    private static void TryDeleteDirectory(string dataPath)
    {
        try
        {
            if (Directory.Exists(dataPath))
            {
                Directory.Delete(dataPath, recursive: true);
            }
        }
        catch
        {
            // best-effort on Windows file locks
        }
    }
}

/// <summary>Static target so Hangfire can serialize without DI in storage-only tests.</summary>
public static class SpikeProbeStatic
{
    public static void Execute()
    {
    }
}
