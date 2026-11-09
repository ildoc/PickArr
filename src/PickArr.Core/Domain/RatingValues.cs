namespace Pickarr.Core.Domain;

public static class RatingValues
{
    public const string Love = "Love";
    public const string Like = "Like";
    public const string Dislike = "Dislike";
    public const string NotInterested = "NotInterested";

    public static bool IsValid(string? value) =>
        string.Equals(value, Love, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Like, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Dislike, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, NotInterested, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, "not_interested", StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string value)
    {
        if (string.Equals(value, Dislike, StringComparison.OrdinalIgnoreCase))
        {
            return Dislike;
        }

        if (string.Equals(value, NotInterested, StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "not_interested", StringComparison.OrdinalIgnoreCase))
        {
            return NotInterested;
        }

        if (string.Equals(value, Love, StringComparison.OrdinalIgnoreCase))
        {
            return Love;
        }

        return Like;
    }

    public static string ToApi(string value)
    {
        if (string.Equals(value, Dislike, StringComparison.OrdinalIgnoreCase))
        {
            return "dislike";
        }

        if (string.Equals(value, NotInterested, StringComparison.OrdinalIgnoreCase))
        {
            return "not_interested";
        }

        if (string.Equals(value, Love, StringComparison.OrdinalIgnoreCase))
        {
            return "love";
        }

        return "like";
    }

    public static string? ToApiOrNull(string? value) =>
        value is null ? null : ToApi(value);

    public static bool IsPositive(string? value) =>
        string.Equals(value, Like, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Love, StringComparison.OrdinalIgnoreCase);
}

public static class RatingEventOrigins
{
    public const string Ui = "Ui";
    public const string Onboarding = "Onboarding";
    public const string Import = "Import";
}
