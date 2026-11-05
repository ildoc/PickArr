using PickArr.Core.Catalog;
using PickArr.Infrastructure.Catalog.Watchmode;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

public sealed class WatchmodeMappingTests
{
    [Theory]
    [InlineData("movie", null, CatalogEntryType.Movie)]
    [InlineData("tv_series", "tv", CatalogEntryType.TvSeries)]
    [InlineData("tv_miniseries", "tv", CatalogEntryType.TvMiniseries)]
    [InlineData("tv_special", "tv", CatalogEntryType.TvSpecial)]
    [InlineData("tv_special", "movie", CatalogEntryType.Movie)]
    public void ToCatalogEntryType_maps_watchmode_and_tmdb(string type, string? tmdb, CatalogEntryType expected)
    {
        WatchmodeMapping.ToCatalogEntryType(type, tmdb).ShouldBe(expected);
    }

    [Fact]
    public void ResolveCredits_uses_header_or_endpoint_defaults()
    {
        using var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("X-Watchmode-Request-Cost", "3");
        WatchmodeApiUsageHandler.ResolveCredits(response, "/list-titles/").ShouldBe(3);

        using var noCost = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        WatchmodeApiUsageHandler.ResolveCredits(noCost, "/status/").ShouldBe(0);
        WatchmodeApiUsageHandler.ResolveCredits(noCost, "/list-titles/").ShouldBe(1);
    }

    [Fact]
    public void NormalizeEndpoint_strips_v1_prefix()
    {
        WatchmodeApiUsageHandler.NormalizeEndpoint(new Uri("https://api.watchmode.com/v1/list-titles/?page=1"))
            .ShouldBe("/list-titles/");
    }
}
