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
    public void AllowedMediaTypes_always_includes_series_and_movies()
    {
        ArrMediaFilter.AllowedMediaTypes()
            .ShouldBe([MediaTypes.Series, MediaTypes.Movie]);
    }
}
