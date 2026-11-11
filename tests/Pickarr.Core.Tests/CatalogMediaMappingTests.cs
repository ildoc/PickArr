using Pickarr.Core.Catalog;
using Pickarr.Core.Domain;
using Shouldly;

namespace Pickarr.Core.Tests;

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
    public void Pending_when_imdb_present_without_tmdb()
    {
        CatalogMediaMapping.InitialMetadataStatus(null, "tt36458668").ShouldBe(MetadataStatuses.Pending);
    }

    [Fact]
    public void Unmapped_when_no_tmdb_or_imdb()
    {
        CatalogMediaMapping.InitialMetadataStatus(null).ShouldBe(MetadataStatuses.Unmapped);
        CatalogMediaMapping.InitialMetadataStatus(null, " ").ShouldBe(MetadataStatuses.Unmapped);
    }
}
