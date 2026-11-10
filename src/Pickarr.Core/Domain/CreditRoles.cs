namespace Pickarr.Core.Domain;

public static class CreditRoles
{
    public const string Cast = "Cast";
    public const string Director = "Director";
    public const string Creator = "Creator";

    public static bool IsValid(string? role) =>
        string.Equals(role, Cast, StringComparison.Ordinal)
        || string.Equals(role, Director, StringComparison.Ordinal)
        || string.Equals(role, Creator, StringComparison.Ordinal);
}
