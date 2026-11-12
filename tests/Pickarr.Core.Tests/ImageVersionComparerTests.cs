using Pickarr.Core.Diagnostics;
using Shouldly;

namespace Pickarr.Core.Tests;

public sealed class ImageVersionComparerTests
{
    [Theory]
    [InlineData("1.2.3", true)]
    [InlineData("1.2.3.4", true)]
    [InlineData("0.0.0.12", true)]
    [InlineData("edge", false)]
    [InlineData("latest", false)]
    [InlineData("v1.2.3", false)]
    [InlineData("1.2", false)]
    [InlineData("", false)]
    public void TryParseVersionTag_accepts_only_X_Y_Z_and_X_Y_Z_N(string tag, bool expected)
    {
        ImageVersionComparer.TryParseVersionTag(tag, out _).ShouldBe(expected);
    }

    [Fact]
    public void ResolveChannel_distinguishes_release_and_development()
    {
        ImageVersionComparer.ResolveChannel("1.2.3").ShouldBe(ImageVersionComparer.ChannelRelease);
        ImageVersionComparer.ResolveChannel("1.2.3.4").ShouldBe(ImageVersionComparer.ChannelDevelopment);
        ImageVersionComparer.ResolveChannel("not-a-version").ShouldBe(ImageVersionComparer.ChannelRelease);
    }

    [Fact]
    public void Release_current_only_surfaces_newest_release_tag()
    {
        var tags = new[]
        {
            "edge",
            "latest",
            "1.2.3",
            "1.2.4",
            "1.3.0",
            "1.2.3.10",
            "2.0.0",
            "1.0.0",
        };

        ImageVersionComparer.FindNewestTag("1.2.3", tags).ShouldBe("2.0.0");
    }

    [Fact]
    public void Development_current_surfaces_newest_versioned_tag()
    {
        var tags = new[]
        {
            "edge",
            "latest",
            "1.2.3",
            "1.2.3.4",
            "1.2.3.10",
            "1.2.4",
            "1.3.0",
            "1.2.3.5",
        };

        ImageVersionComparer.FindNewestTag("1.2.3.4", tags).ShouldBe("1.3.0");
    }

    [Fact]
    public void Equal_or_older_tags_are_excluded()
    {
        ImageVersionComparer.FindNewestTag("1.2.3", ["1.2.3", "1.2.2", "1.1.9"])
            .ShouldBeNull();

        ImageVersionComparer.FindNewestTag("1.2.3.10", ["1.2.3.10", "1.2.3.9", "1.2.3"])
            .ShouldBeNull();
    }

    [Fact]
    public void Invalid_current_version_yields_no_updates()
    {
        ImageVersionComparer.FindNewestTag("edge", ["1.2.3", "2.0.0"])
            .ShouldBeNull();
    }

    [Fact]
    public void Zero_prefix_development_builds_compare_numerically()
    {
        ImageVersionComparer.FindNewestTag("0.0.0.32", ["edge", "0.0.0.32", "0.0.0.33"])
            .ShouldBe("0.0.0.33");
    }
}
