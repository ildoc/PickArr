using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Pickarr.Core.Abstractions;
using Pickarr.Core.Diagnostics;
using Pickarr.Infrastructure.Diagnostics;
using Shouldly;

namespace Pickarr.Infrastructure.Tests;

public sealed class ImageUpdateServiceTests
{
    [Fact]
    public async Task CheckAsync_filters_release_channel_to_release_tags_only()
    {
        var source = Substitute.For<IImageTagSource>();
        source.ListTagsAsync(Arg.Any<CancellationToken>())
            .Returns(ImageTagListResult.Ok(["edge", "1.2.3", "1.2.4", "1.2.3.10", "1.3.0"]));

        var sut = CreateSut(source);
        var dto = await sut.CheckAsync("1.2.3");

        dto.Channel.ShouldBe(ImageVersionComparer.ChannelRelease);
        dto.UpdateAvailable.ShouldBeTrue();
        dto.CheckSucceeded.ShouldBeTrue();
        dto.NewerTags.ShouldBe(["1.3.0", "1.2.4"]);
        dto.Source.ShouldBe("ghcr.io/ildoc/pickarr");
        dto.CurrentVersion.ShouldBe("1.2.3");
    }

    [Fact]
    public async Task CheckAsync_development_includes_versioned_tags()
    {
        var source = Substitute.For<IImageTagSource>();
        source.ListTagsAsync(Arg.Any<CancellationToken>())
            .Returns(ImageTagListResult.Ok(["1.2.3.5", "1.2.4", "edge", "1.2.3.4"]));

        var sut = CreateSut(source);
        var dto = await sut.CheckAsync("1.2.3.4");

        dto.Channel.ShouldBe(ImageVersionComparer.ChannelDevelopment);
        dto.NewerTags.ShouldBe(["1.2.4", "1.2.3.5"]);
        dto.UpdateAvailable.ShouldBeTrue();
        dto.CheckSucceeded.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckAsync_detects_newer_zero_prefix_dev_build()
    {
        var source = Substitute.For<IImageTagSource>();
        source.ListTagsAsync(Arg.Any<CancellationToken>())
            .Returns(ImageTagListResult.Ok(["edge", "0.0.0.32", "0.0.0.33"]));

        var sut = CreateSut(source);
        var dto = await sut.CheckAsync("0.0.0.32");

        dto.UpdateAvailable.ShouldBeTrue();
        dto.NewerTags.ShouldBe(["0.0.0.33"]);
    }

    [Fact]
    public async Task CheckAsync_marks_check_failed_when_tag_source_fails()
    {
        var source = Substitute.For<IImageTagSource>();
        source.ListTagsAsync(Arg.Any<CancellationToken>()).Returns(ImageTagListResult.Failed());

        var sut = CreateSut(source);
        var dto = await sut.CheckAsync("1.0.0");

        dto.UpdateAvailable.ShouldBeFalse();
        dto.CheckSucceeded.ShouldBeFalse();
        dto.NewerTags.ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckAsync_caches_successful_tag_list_only()
    {
        var source = Substitute.For<IImageTagSource>();
        source.ListTagsAsync(Arg.Any<CancellationToken>())
            .Returns(
                ImageTagListResult.Failed(),
                ImageTagListResult.Ok(["1.1.0"]),
                ImageTagListResult.Ok(["1.1.0"]));

        var sut = CreateSut(source);

        (await sut.CheckAsync("1.0.0")).CheckSucceeded.ShouldBeFalse();
        (await sut.CheckAsync("1.0.0")).UpdateAvailable.ShouldBeTrue();
        (await sut.CheckAsync("1.0.0")).UpdateAvailable.ShouldBeTrue();

        // Failed fetch is not cached; successful fetch is reused once.
        await source.Received(2).ListTagsAsync(Arg.Any<CancellationToken>());
    }

    private static ImageUpdateService CreateSut(IImageTagSource source)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc));
        return new ImageUpdateService(
            source,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new GhcrOptions()),
            clock,
            NullLogger<ImageUpdateService>.Instance);
    }
}
