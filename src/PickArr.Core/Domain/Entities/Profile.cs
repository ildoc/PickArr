namespace Pickarr.Core.Domain.Entities;

public sealed class Profile
{
    public const string AdminName = "Admin";
    public const string DefaultColor = "blue";
    public const int MaxNameLength = 40;
    public const int MaxColorLength = 32;
    public const int MaxProfiles = 10;

    public static readonly string[] Palette =
    [
        "blue",
        "teal",
        "green",
        "yellow",
        "orange",
        "red",
        "pink",
        "purple",
    ];

    public static bool TryCanonicalColor(string value, out string canonical)
    {
        foreach (var color in Palette)
        {
            if (string.Equals(color, value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                canonical = color;
                return true;
            }
        }

        canonical = "";
        return false;
    }

    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Color { get; set; }
    public bool IsAdmin { get; set; }
    public bool UseImplicit { get; set; } = true;
    public bool ExcludeInLibrary { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
