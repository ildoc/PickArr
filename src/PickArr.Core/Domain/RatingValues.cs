namespace PickArr.Core.Domain;

public static class RatingValues
{
    public const string Love = "Love";
    public const string Like = "Like";
    public const string Dislike = "Dislike";

    public static bool IsValid(string? value) =>
        string.Equals(value, Love, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Like, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Dislike, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string value)
    {
        if (string.Equals(value, Dislike, StringComparison.OrdinalIgnoreCase))
        {
            return Dislike;
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
