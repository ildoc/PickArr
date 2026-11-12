namespace Pickarr.Core.Diagnostics;

public sealed record ImageUpdateDto(
    string CurrentVersion,
    string Channel,
    string? NewestTag,
    bool UpdateAvailable,
    bool CheckSucceeded,
    DateTimeOffset CheckedAt,
    string Source);
