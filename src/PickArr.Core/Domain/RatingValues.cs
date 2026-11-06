namespace PickArr.Core.Domain;

public static class RatingValues
{
    public const string Like = "Like";
    public const string Dislike = "Dislike";

    public static bool IsValid(string? value) =>
        string.Equals(value, Like, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Dislike, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string value) =>
        string.Equals(value, Dislike, StringComparison.OrdinalIgnoreCase) ? Dislike : Like;

    public static string ToApi(string value) =>
        string.Equals(value, Dislike, StringComparison.OrdinalIgnoreCase) ? "dislike" : "like";

    public static string? ToApiOrNull(string? value) =>
        value is null ? null : ToApi(value);
}

public static class RatingEventOrigins
{
    public const string Ui = "Ui";
    public const string Onboarding = "Onboarding";
    public const string Import = "Import";
}
