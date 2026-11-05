using PickArr.Core.Catalog;
using PickArr.Core.Domain;
using Shouldly;

namespace PickArr.Core.Tests;

public sealed class CatalogMediaMappingTests
{
    [Theory]
    [InlineData(CatalogEntryType.Movie, MediaTypes.Movie, MetadataStatuses.Pending)]
    [InlineData(CatalogEntryType.TvSeries, MediaTypes.Series, MetadataStatuses.Pending)]
    public void Maps_media_and_pending_when_tmdb_present(CatalogEntryType type, string media, string status)
    {
        CatalogMediaMapping.ToMediaType(type).ShouldBe(media);
        CatalogMediaMapping.InitialMetadataStatus(123).ShouldBe(status);
    }

    [Fact]
    public void Unmapped_when_no_tmdb_id()
    {
        CatalogMediaMapping.InitialMetadataStatus(null).ShouldBe(MetadataStatuses.Unmapped);
    }
}
