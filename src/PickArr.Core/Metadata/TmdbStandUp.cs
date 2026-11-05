namespace PickArr.Core.Metadata;

/// <summary>SP-2: stand-up classification via TMDB keywords (D-008).</summary>
public static class TmdbStandUp
{
    /// <summary>Typical TMDB keyword id for "stand-up comedy"; confirm live via /search/keyword.</summary>
    public const int KeywordId = 9715;

    public static bool IsStandUpKeyword(int id, string? name)
    {
        if (id == KeywordId)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var normalized = name.Trim().ToLowerInvariant().Replace('–', '-').Replace('—', '-');
        normalized = normalized.Replace(" ", "").Replace("-", "");
        return normalized is "standupcomedy" or "standup";
    }
}
