namespace Pickarr.Core.Diagnostics;

public sealed record ImageUpdateDto(
    string CurrentVersion,
    string Channel,
    IReadOnlyList<string> NewerTags,
    bool UpdateAvailable,
    bool CheckSucceeded,
    DateTimeOffset CheckedAt,
    string Source);
