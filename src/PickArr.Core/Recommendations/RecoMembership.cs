namespace Pickarr.Core.Recommendations;

/// <summary>
/// Membership for recommendations (BR-REC-07). Distinct from catalog grid filters:
/// <c>RemovedAt</c> is irrelevant — presence of an Availability row is enough.
/// </summary>
public static class RecoMembership
{
    public const string ScopeSource = "source";
    public const string ScopeEnabled = "enabled";

    public static bool IsValidScope(string? scope) =>
        string.Equals(scope, ScopeSource, StringComparison.OrdinalIgnoreCase)
        || string.Equals(scope, ScopeEnabled, StringComparison.OrdinalIgnoreCase);

    public static string NormalizeScope(string? scope) =>
        string.Equals(scope, ScopeEnabled, StringComparison.OrdinalIgnoreCase)
            ? ScopeEnabled
            : ScopeSource;

    /// <summary>Scope <c>source</c>: any Availability for the catalog source (RemovedAt ignored).</summary>
    public static bool BelongsToSource(
        IEnumerable<RecoAvailabilityFact> availabilities,
        int titleId,
        int catalogSourceId) =>
        availabilities.Any(a => a.TitleId == titleId && a.CatalogSourceId == catalogSourceId);

    /// <summary>
    /// Scope <c>enabled</c> / pool: Availability whose CatalogSource and Provider are both enabled
    /// (RemovedAt ignored).
    /// </summary>
    public static bool BelongsToEnabled(
        IEnumerable<RecoAvailabilityFact> availabilities,
        IEnumerable<RecoSourceFact> sources,
        int titleId)
    {
        var enabledSourceIds = EnabledSourceIds(sources);
        return availabilities.Any(a => a.TitleId == titleId && enabledSourceIds.Contains(a.CatalogSourceId));
    }

    /// <summary>Pool title ids: at least one Availability on an enabled source+provider.</summary>
    public static IReadOnlyList<int> PoolTitleIds(
        IEnumerable<RecoAvailabilityFact> availabilities,
        IEnumerable<RecoSourceFact> sources)
    {
        var enabledSourceIds = EnabledSourceIds(sources);
        return availabilities
            .Where(a => enabledSourceIds.Contains(a.CatalogSourceId))
            .Select(a => a.TitleId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
    }

    public static HashSet<int> EnabledSourceIds(IEnumerable<RecoSourceFact> sources) =>
        sources
            .Where(s => s.SourceEnabled && s.ProviderEnabled)
            .Select(s => s.CatalogSourceId)
            .ToHashSet();
}

public readonly record struct RecoAvailabilityFact(int TitleId, int CatalogSourceId);

public readonly record struct RecoSourceFact(
    int CatalogSourceId,
    bool SourceEnabled,
    bool ProviderEnabled);
