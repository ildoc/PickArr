namespace Pickarr.Core.Metadata;

public static class LanguageFallback
{
    public const string English = "en-US";

    public static bool NeedsEnglishFallback(string? title, string? overview) =>
        string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(overview);
}
