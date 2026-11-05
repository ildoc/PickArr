namespace PickArr.Infrastructure.Jobs;

public sealed class HangfireOptions
{
    public const string SectionName = "Hangfire";

    /// <summary>
    /// Preferred storage. SQLite is default; Memory is the documented fallback (V-09 / D-013).
    /// Application recurring schedules must be re-applied from CatalogSources via ScheduleReconciler
    /// so that either storage remains safe across restarts.
    /// </summary>
    public HangfireStorageKind Storage { get; set; } = HangfireStorageKind.Sqlite;

    public bool DashboardEnabled { get; set; }

    /// <summary>File name under PickArr data path when Storage=Sqlite.</summary>
    public string SqliteFileName { get; set; } = "jobs.db";
}
