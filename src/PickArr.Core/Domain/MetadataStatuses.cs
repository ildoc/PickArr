namespace PickArr.Core.Domain;

public static class MetadataStatuses
{
    public const string Pending = "Pending";
    public const string Ready = "Ready";
    public const string Unmapped = "Unmapped";
    public const string Failed = "Failed";
}

public static class MediaTypes
{
    public const string Movie = "Movie";
    public const string Series = "Series";
}

public static class TitleCategories
{
    public const string Movie = "Movie";
    public const string Series = "Series";
    public const string StandUp = "StandUp";
}

public static class AvailabilityEventTypes
{
    public const string Added = "Added";
    public const string Removed = "Removed";
    public const string Readded = "Readded";
    public const string LeavingAnnounced = "LeavingAnnounced";
    public const string LeavingCleared = "LeavingCleared";
}
