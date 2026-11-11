using System.Globalization;
using System.Text.RegularExpressions;

namespace Pickarr.Core.Diagnostics;

/// <summary>
/// Compares the running image version against GHCR version tags.
/// Release builds (<c>X.Y.Z</c>) only surface newer release tags; development
/// builds (<c>X.Y.Z.N</c>) surface any numerically greater versioned tag.
/// Floating tags such as <c>edge</c> and <c>latest</c> are ignored.
/// </summary>
public static partial class ImageVersionComparer
{
    public const string ChannelRelease = "release";
    public const string ChannelDevelopment = "development";

    [GeneratedRegex(@"^\d+\.\d+\.\d+(\.\d+)?$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionTagPattern();

    public static bool TryParseVersionTag(string tag, out Version version)
    {
        version = null!;
        if (string.IsNullOrWhiteSpace(tag) || !VersionTagPattern().IsMatch(tag))
        {
            return false;
        }

        return Version.TryParse(tag, out version!);
    }

    public static bool IsRelease(Version version) => version.Revision < 0;

    public static string ResolveChannel(Version version) =>
        IsRelease(version) ? ChannelRelease : ChannelDevelopment;

    public static string ResolveChannel(string currentVersion) =>
        TryParseVersionTag(currentVersion, out var version)
            ? ResolveChannel(version)
            : ChannelRelease;

    /// <summary>
    /// Returns version tags strictly newer than <paramref name="currentVersion"/>,
    /// sorted descending (newest first). Original tag strings are preserved.
    /// </summary>
    public static IReadOnlyList<string> FindNewerTags(string currentVersion, IEnumerable<string> tags)
    {
        if (!TryParseVersionTag(currentVersion, out var current))
        {
            return [];
        }

        var releaseOnly = IsRelease(current);
        var currentKey = ToComparable(current);
        var newer = new List<(Version Key, string Tag)>();

        foreach (var tag in tags)
        {
            if (!TryParseVersionTag(tag, out var candidate))
            {
                continue;
            }

            if (releaseOnly && !IsRelease(candidate))
            {
                continue;
            }

            var candidateKey = ToComparable(candidate);
            if (candidateKey <= currentKey)
            {
                continue;
            }

            newer.Add((candidateKey, tag));
        }

        return newer
            .OrderByDescending(x => x.Key)
            .ThenByDescending(x => x.Tag, StringComparer.Ordinal)
            .Select(x => x.Tag)
            .ToArray();
    }

    /// <summary>
    /// Normalizes to four components so <see cref="Version"/> can compare
    /// release (<c>X.Y.Z</c>) and development (<c>X.Y.Z.N</c>) tags safely.
    /// </summary>
    internal static Version ToComparable(Version version) =>
        new(
            version.Major,
            version.Minor,
            version.Build < 0 ? 0 : version.Build,
            version.Revision < 0 ? 0 : version.Revision);

    public static string FormatVersion(Version version) =>
        IsRelease(version)
            ? string.Create(CultureInfo.InvariantCulture, $"{version.Major}.{version.Minor}.{version.Build}")
            : version.ToString();
}
