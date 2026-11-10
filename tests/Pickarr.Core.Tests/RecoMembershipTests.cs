using Pickarr.Core.Recommendations;
using Shouldly;

namespace Pickarr.Core.Tests;

public sealed class RecoMembershipTests
{
    [Fact]
    public void BelongsToSource_includes_title_even_when_conceptually_removed()
    {
        // RemovedAt is not part of the fact — presence alone is enough (BR-REC-07 / RR-06).
        var availabilities = new[]
        {
            new RecoAvailabilityFact(TitleId: 10, CatalogSourceId: 1),
        };

        RecoMembership.BelongsToSource(availabilities, titleId: 10, catalogSourceId: 1).ShouldBeTrue();
        RecoMembership.BelongsToSource(availabilities, titleId: 10, catalogSourceId: 2).ShouldBeFalse();
        RecoMembership.BelongsToSource(availabilities, titleId: 99, catalogSourceId: 1).ShouldBeFalse();
    }

    [Fact]
    public void BelongsToEnabled_excludes_disabled_source_or_provider()
    {
        var availabilities = new[]
        {
            new RecoAvailabilityFact(10, CatalogSourceId: 1),
            new RecoAvailabilityFact(11, CatalogSourceId: 2),
            new RecoAvailabilityFact(12, CatalogSourceId: 3),
        };
        var sources = new[]
        {
            new RecoSourceFact(1, SourceEnabled: true, ProviderEnabled: true),
            new RecoSourceFact(2, SourceEnabled: false, ProviderEnabled: true),
            new RecoSourceFact(3, SourceEnabled: true, ProviderEnabled: false),
        };

        RecoMembership.BelongsToEnabled(availabilities, sources, 10).ShouldBeTrue();
        RecoMembership.BelongsToEnabled(availabilities, sources, 11).ShouldBeFalse();
        RecoMembership.BelongsToEnabled(availabilities, sources, 12).ShouldBeFalse();
    }

    [Fact]
    public void PoolTitleIds_excludes_zero_availability_and_disabled_sources()
    {
        var availabilities = new[]
        {
            new RecoAvailabilityFact(10, 1),
            new RecoAvailabilityFact(11, 2),
            new RecoAvailabilityFact(10, 2), // same title, disabled source — still in pool via source 1
        };
        var sources = new[]
        {
            new RecoSourceFact(1, true, true),
            new RecoSourceFact(2, false, true),
        };

        var pool = RecoMembership.PoolTitleIds(availabilities, sources);
        pool.ShouldBe([10]);
        // Title 99 has zero Availability → excluded
        pool.ShouldNotContain(99);
        pool.ShouldNotContain(11);
    }

    [Fact]
    public void NormalizeScope_defaults_to_source()
    {
        RecoMembership.NormalizeScope(null).ShouldBe(RecoMembership.ScopeSource);
        RecoMembership.NormalizeScope("ENABLED").ShouldBe(RecoMembership.ScopeEnabled);
        RecoMembership.NormalizeScope("source").ShouldBe(RecoMembership.ScopeSource);
    }
}
