using Microsoft.EntityFrameworkCore;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Diagnostics;
using Pickarr.Infrastructure.Jobs;
using Pickarr.Infrastructure.Persistence;

namespace Pickarr.Infrastructure.Diagnostics;

public sealed class SystemStatsService(
    PickarrDbContext db,
    PickarrDataPaths paths,
    HangfireRuntimeInfo hangfire) : ISystemStatsService
{
    public async Task<SystemStatsDto> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        var totalTitles = await db.Titles.AsNoTracking().LongCountAsync(cancellationToken);

        var active = db.Availabilities.AsNoTracking()
            .Where(a => a.RemovedAt == null)
            .Select(a => new
            {
                a.TitleId,
                ProviderName = a.CatalogSource!.Provider!.DisplayName,
                CountryCode = a.CatalogSource.CountryCode,
            });

        var byProviderRows = await active
            .GroupBy(a => a.ProviderName)
            .Select(g => new { Name = g.Key, TitleCount = g.Select(x => x.TitleId).Distinct().LongCount() })
            .ToListAsync(cancellationToken);

        var byCountryRows = await active
            .GroupBy(a => a.CountryCode)
            .Select(g => new { Name = g.Key, TitleCount = g.Select(x => x.TitleId).Distinct().LongCount() })
            .ToListAsync(cancellationToken);

        var bySourceRows = await active
            .GroupBy(a => new { a.ProviderName, a.CountryCode })
            .Select(g => new
            {
                g.Key.ProviderName,
                g.Key.CountryCode,
                TitleCount = g.Select(x => x.TitleId).Distinct().LongCount(),
            })
            .ToListAsync(cancellationToken);

        var byProvider = byProviderRows
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => new NamedTitleCountDto(r.Name, r.TitleCount))
            .ToList();

        var byCountry = byCountryRows
            .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
            .Select(r => new NamedTitleCountDto(r.Name.Trim().ToUpperInvariant(), r.TitleCount))
            .ToList();

        var bySource = bySourceRows
            .OrderBy(r => r.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.CountryCode, StringComparer.OrdinalIgnoreCase)
            .Select(r => new SourceTitleCountDto(
                r.ProviderName,
                r.CountryCode.Trim().ToUpperInvariant(),
                r.TitleCount))
            .ToList();

        return new SystemStatsDto(
            totalTitles,
            byProvider,
            byCountry,
            bySource,
            BuildStorage());
    }

    private SystemStorageDto BuildStorage()
    {
        var entries = new List<StorageEntryDto>
        {
            Entry("database", Rel(paths.DatabaseFile), SqliteBundleSize(paths.DatabaseFile)),
        };

        if (hangfire.RequestedStorage == HangfireStorageKind.Sqlite)
        {
            var jobsPath = hangfire.SqlitePath ?? paths.JobsDatabaseFile;
            if (!string.IsNullOrWhiteSpace(jobsPath))
            {
                entries.Add(Entry("jobs", Rel(jobsPath), SqliteBundleSize(jobsPath)));
            }
        }

        entries.Add(Entry("logs", Rel(paths.LogsDirectory), DirectorySize(paths.LogsDirectory)));
        entries.Add(Entry("models", Rel(paths.ModelsDirectory), DirectorySize(paths.ModelsDirectory)));
        entries.Add(Entry("keys", Rel(paths.KeysDirectory), DirectorySize(paths.KeysDirectory)));

        var accounted = entries.Sum(e => e.Bytes);
        var rootTotal = DirectorySize(paths.Root);
        var other = Math.Max(0, rootTotal - accounted);
        if (other > 0)
        {
            entries.Add(Entry("other", Rel(paths.Root), other));
        }

        return new SystemStorageDto(rootTotal, entries);
    }

    private string? Rel(string absolute)
    {
        var root = Path.GetFullPath(paths.Root)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var full = Path.GetFullPath(absolute);
        if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            var relative = full[root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrEmpty(relative) ? "." : relative.Replace('\\', '/');
        }

        return Path.GetFileName(absolute);
    }

    private static StorageEntryDto Entry(string key, string? relativePath, long bytes) =>
        new(key, relativePath, bytes);

    private static long SqliteBundleSize(string dbPath)
    {
        long total = FileSize(dbPath);
        total += FileSize(dbPath + "-wal");
        total += FileSize(dbPath + "-shm");
        return total;
    }

    private static long FileSize(string path)
    {
        try
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }

    private static long DirectorySize(string path)
    {
        try
        {
            if (!Directory.Exists(path)) return 0;
            return new DirectoryInfo(path)
                .EnumerateFiles("*", SearchOption.AllDirectories)
                .Sum(f =>
                {
                    try
                    {
                        return f.Length;
                    }
                    catch (IOException)
                    {
                        return 0;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return 0;
                    }
                });
        }
        catch (IOException)
        {
            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            return 0;
        }
    }
}
