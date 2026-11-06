using Hangfire;
using Hangfire.InMemory;
using Hangfire.Storage.SQLite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PickArr.Infrastructure.Jobs;

public static class HangfireBootstrap
{
    public static IServiceCollection AddPickArrHangfire(
        this IServiceCollection services,
        IConfiguration configuration,
        string dataPath)
    {
        services.Configure<HangfireOptions>(configuration.GetSection(HangfireOptions.SectionName));

        var options = configuration.GetSection(HangfireOptions.SectionName).Get<HangfireOptions>()
                      ?? new HangfireOptions();

        services.AddHangfire((sp, global) =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("PickArr.Hangfire");
            ConfigureStorage(global, options, dataPath, logger);
        });

        services.AddHangfireServer(server =>
        {
            server.Queues = ["default", "sync", "arr"];
            server.WorkerCount = Math.Max(1, Environment.ProcessorCount / 2);
        });

        // Isolated ML queue so long reco/embedding work does not occupy sync/arr workers (§11.1).
        services.AddHangfireServer(server =>
        {
            server.Queues = ["ml"];
            server.WorkerCount = 1;
            server.ServerName = $"{Environment.MachineName}:ml";
        });

        services.AddSingleton(new HangfireRuntimeInfo(options.Storage, ResolveSqlitePath(dataPath, options)));
        return services;
    }

    public static void ConfigureStorage(
        IGlobalConfiguration global,
        HangfireOptions options,
        string dataPath,
        ILogger logger)
    {
        if (options.Storage == HangfireStorageKind.Memory)
        {
            global.UseInMemoryStorage();
            logger.LogInformation("Hangfire storage: InMemory (configured)");
            return;
        }

        var sqlitePath = ResolveSqlitePath(dataPath, options);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(sqlitePath)!);
            global.UseSQLiteStorage(sqlitePath, new SQLiteStorageOptions
            {
                JournalMode = SQLiteStorageOptions.JournalModes.WAL,
                QueuePollInterval = TimeSpan.FromSeconds(2),
            });
            logger.LogInformation("Hangfire storage: SQLite at {Path}", sqlitePath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Hangfire SQLite storage failed; falling back to InMemory");
            global.UseInMemoryStorage();
        }
    }

    public static string ResolveSqlitePath(string dataPath, HangfireOptions options)
        => Path.Combine(dataPath, options.SqliteFileName);
}

/// <summary>Probeable runtime choice for tests and readiness.</summary>
public sealed record HangfireRuntimeInfo(HangfireStorageKind RequestedStorage, string? SqlitePath);
