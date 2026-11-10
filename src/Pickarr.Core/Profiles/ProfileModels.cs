namespace Pickarr.Core.Profiles;

public static class ProfileHeader
{
    public const string Name = "X-Pickarr-Profile";
}

public static class ProfileCommandCodes
{
    public const string Forbidden = "PROFILE_FORBIDDEN";
    public const string NotFound = "PROFILE_NOT_FOUND";
    public const string Limit = "PROFILE_LIMIT";
    public const string AdminUndeletable = "PROFILE_ADMIN";
    public const string Invalid = "PROFILE_INVALID";
}

public sealed class ProfileCommandException : Exception
{
    public ProfileCommandException(string code, string detail)
        : base(detail)
    {
        Code = code;
    }

    public string Code { get; }
}

public sealed record ProfileDto(
    int Id,
    string Name,
    string Color,
    bool IsAdmin,
    bool UseImplicit,
    bool ExcludeInLibrary,
    DateTime CreatedAt);

public sealed record ProfileCreateRequest(string? Name, string? Color);

public sealed record ProfileUpdateRequest(
    string? Name,
    string? Color,
    bool? UseImplicit,
    bool? ExcludeInLibrary,
    bool? IsAdmin);
