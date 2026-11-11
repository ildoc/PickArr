using Pickarr.Core.Domain;

namespace Pickarr.Core.Metadata;

/// <summary>
/// Stand-up classification from TMDB keyword <em>names</em> (D-06 / BR-02).
/// Does not hardcode TMDB keyword ids — those can collide or drift.
/// </summary>
public static class TmdbStandUp
{
    public static bool IsStandUpKeyword(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        // "stand-up comedy", "standup comedy", "stand–up comedy", …
        var normalized = name.Trim().ToLowerInvariant()
            .Replace('–', '-')
            .Replace('—', '-')
            .Replace(" ", "")
            .Replace("-", "")
            .Replace("_", "");
        return normalized == "standupcomedy";
    }

    public static bool HasStandUpKeyword(IEnumerable<string?> names) =>
        names.Any(IsStandUpKeyword);

    /// <summary>
    /// UI category: StandUp when a stand-up comedy keyword is present; otherwise Movie/Series from media type.
    /// </summary>
    public static string ResolveCategory(string mediaType, IEnumerable<string?> keywordNames)
    {
        if (HasStandUpKeyword(keywordNames))
        {
            return TitleCategories.StandUp;
        }

        return mediaType == MediaTypes.Movie ? TitleCategories.Movie : TitleCategories.Series;
    }
}
