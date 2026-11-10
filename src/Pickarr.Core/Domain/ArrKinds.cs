namespace Pickarr.Core.Domain;

public static class ArrKinds
{
    public const string Sonarr = "Sonarr";
    public const string Radarr = "Radarr";
}

public static class ArrStates
{
    public const string NotInLibrary = "NotInLibrary";
    public const string Missing = "Missing";
    public const string Downloading = "Downloading";
    public const string Downloaded = "Downloaded";
    public const string Partial = "Partial";
}

public enum ArrKind
{
    Sonarr,
    Radarr,
}

public enum ArrState
{
    NotInLibrary,
    Missing,
    Downloading,
    Downloaded,
    Partial,
}
