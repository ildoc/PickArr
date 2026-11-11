using Microsoft.Extensions.Logging;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Diagnostics;

namespace Pickarr.Infrastructure.Diagnostics;

public sealed class LogFileService(
    PickarrDataPaths paths,
    ILogger<LogFileService> logger) : ILogFileService
{
    public Task<LogFilesListDto> ListAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(paths.LogsDirectory);
        var items = Directory.EnumerateFiles(paths.LogsDirectory)
            .Select(path => new FileInfo(path))
            .Where(file => IsAllowedLogName(file.Name))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new LogFileInfoDto(
                file.Name,
                file.Length,
                new DateTimeOffset(DateTime.SpecifyKind(file.LastWriteTimeUtc, DateTimeKind.Utc))))
            .ToList();

        return Task.FromResult(new LogFilesListDto(items));
    }

    public Task<(Stream Stream, string ContentType, string DownloadName)?> OpenReadAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryResolveLogPath(fileName, out var fullPath))
        {
            return Task.FromResult<(Stream, string, string)?>(null);
        }

        Stream stream = new FileStream(
            fullPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return Task.FromResult<(Stream, string, string)?>((stream, "text/plain", Path.GetFileName(fullPath)));
    }

    public Task<ClearLogsResultDto> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(paths.LogsDirectory);
        var deleted = 0;
        var failed = 0;
        foreach (var path in Directory.EnumerateFiles(paths.LogsDirectory))
        {
            var name = Path.GetFileName(path);
            if (!IsAllowedLogName(name))
            {
                continue;
            }

            try
            {
                File.Delete(path);
                deleted++;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogWarning(ex, "Could not delete log file {File}", name);
            }
        }

        return Task.FromResult(new ClearLogsResultDto(deleted, failed));
    }

    private bool TryResolveLogPath(string fileName, out string fullPath)
    {
        fullPath = "";
        if (!IsAllowedLogName(fileName))
        {
            return false;
        }

        var candidate = Path.GetFullPath(Path.Combine(paths.LogsDirectory, fileName));
        var root = Path.GetFullPath(paths.LogsDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(
                Path.GetDirectoryName(candidate),
                Path.GetFullPath(paths.LogsDirectory),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!File.Exists(candidate))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    private static bool IsAllowedLogName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        if (fileName.Contains('/') || fileName.Contains('\\') || fileName.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        // Serilog rolling: pickarr-20261111.log, pickarr-20261111_001.log, etc.
        return fileName.StartsWith("pickarr-", StringComparison.OrdinalIgnoreCase)
               && fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase);
    }
}
