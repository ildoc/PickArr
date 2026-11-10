using Pickarr.Core.Catalog;

namespace Pickarr.Core.Abstractions;

public sealed record UpsertPageResult(int TitlesAdded, int TitlesUpdated, int AvailabilitiesAdded, int AvailabilitiesReadded);

public interface ICatalogUpserter
{
    Task<UpsertPageResult> UpsertPageAsync(
        int catalogSourceId,
        IReadOnlyList<CatalogEntry> entries,
        Guid? syncRunId,
        bool baselineCompleted,
        CancellationToken cancellationToken = default);
}
