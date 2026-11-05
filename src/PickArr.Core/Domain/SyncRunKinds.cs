namespace PickArr.Core.Domain;

public static class SyncRunKinds
{
    public const string CatalogIncremental = "CatalogIncremental";
    public const string CatalogFull = "CatalogFull";
    public const string Enrichment = "Enrichment";
    public const string MetadataRefresh = "MetadataRefresh";
    public const string ArrSync = "ArrSync";
    public const string Leaving = "Leaving";
}

public static class SyncRunStatuses
{
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string PartiallySucceeded = "PartiallySucceeded";
    public const string Paused = "Paused";
    public const string NeedsReview = "NeedsReview";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
}

public static class SyncRunTriggers
{
    public const string Schedule = "Schedule";
    public const string Manual = "Manual";
    public const string Resume = "Resume";
    public const string Webhook = "Webhook";
}
