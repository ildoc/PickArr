namespace Pickarr.Core.Diagnostics;

public sealed record LogFileInfoDto(
    string FileName,
    long Bytes,
    DateTimeOffset LastWriteUtc);

public sealed record LogFilesListDto(IReadOnlyList<LogFileInfoDto> Items);

public sealed record ClearLogsResultDto(int Deleted, int Failed);
