namespace Pickarr.Core.Domain;

public static class RecoEventKinds
{
    public const string Shown = "Shown";
    public const string Opened = "Opened";
    public const string Liked = "Liked";
    public const string Loved = "Loved";
    public const string Disliked = "Disliked";
    public const string NotInterested = "NotInterested";
    public const string AddedToArr = "AddedToArr";

    public static bool IsValid(string? value) =>
        string.Equals(value, Shown, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Opened, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Liked, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Loved, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, Disliked, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, NotInterested, StringComparison.OrdinalIgnoreCase)
        || string.Equals(value, AddedToArr, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string value)
    {
        if (string.Equals(value, Opened, StringComparison.OrdinalIgnoreCase)) return Opened;
        if (string.Equals(value, Liked, StringComparison.OrdinalIgnoreCase)) return Liked;
        if (string.Equals(value, Loved, StringComparison.OrdinalIgnoreCase)) return Loved;
        if (string.Equals(value, Disliked, StringComparison.OrdinalIgnoreCase)) return Disliked;
        if (string.Equals(value, NotInterested, StringComparison.OrdinalIgnoreCase)) return NotInterested;
        if (string.Equals(value, AddedToArr, StringComparison.OrdinalIgnoreCase)) return AddedToArr;
        return Shown;
    }
}
