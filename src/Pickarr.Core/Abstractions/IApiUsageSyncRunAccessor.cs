namespace Pickarr.Core.Abstractions;

/// <summary>
/// Optional ambient SyncRun id for ApiUsage rows written by HTTP handlers.
/// </summary>
public interface IApiUsageSyncRunAccessor
{
    Guid? CurrentSyncRunId { get; set; }
}
