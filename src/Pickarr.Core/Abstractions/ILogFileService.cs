using Pickarr.Core.Diagnostics;

namespace Pickarr.Core.Abstractions;

public interface ILogFileService
{
    Task<LogFilesListDto> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Opens a log file for download. Caller must dispose the stream.</summary>
    Task<(Stream Stream, string ContentType, string DownloadName)?> OpenReadAsync(
        string fileName,
        CancellationToken cancellationToken = default);

    Task<ClearLogsResultDto> ClearAllAsync(CancellationToken cancellationToken = default);
}
