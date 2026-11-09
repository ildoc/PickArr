using Pickarr.Core.Abstractions;

namespace Pickarr.Infrastructure.Catalog;

public sealed class ApiUsageSyncRunAccessor : IApiUsageSyncRunAccessor
{
    private static readonly AsyncLocal<Guid?> Current = new();

    public Guid? CurrentSyncRunId
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
