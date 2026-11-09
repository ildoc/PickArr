using Pickarr.Core.Catalog;
using Pickarr.Core.Domain;
using Shouldly;

namespace Pickarr.Core.Tests;

public sealed class ArrMediaFilterTests
{
    [Theory]
    [InlineData(null, false, false)]
    [InlineData("", true, false)]
    [InlineData("http://sonarr", false, false)]
    [InlineData("http://sonarr", true, true)]
    public void IsConfigured_requires_url_and_api_key(string? url, bool apiKeySet, bool expected)
    {
        ArrMediaFilter.IsConfigured(url, apiKeySet).ShouldBe(expected);
    }

    [Fact]
    public void AllowedMediaTypes_follows_V08()
    {
        ArrMediaFilter.AllowedMediaTypes(sonarrConfigured: false, radarrConfigured: false)
            .ShouldBeEmpty();

        ArrMediaFilter.AllowedMediaTypes(sonarrConfigured: true, radarrConfigured: false)
            .ShouldBe([MediaTypes.Series]);

        ArrMediaFilter.AllowedMediaTypes(sonarrConfigured: false, radarrConfigured: true)
            .ShouldBe([MediaTypes.Movie]);

        ArrMediaFilter.AllowedMediaTypes(sonarrConfigured: true, radarrConfigured: true)
            .ShouldBe([MediaTypes.Series, MediaTypes.Movie]);
    }
}
