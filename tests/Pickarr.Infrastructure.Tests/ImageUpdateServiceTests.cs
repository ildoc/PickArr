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
            .Returns(["edge", "1.2.3", "1.2.4", "1.2.3.10", "1.3.0"]);

        var sut = CreateSut(source);
        var dto = await sut.CheckAsync("1.2.3");

        dto.Channel.ShouldBe(ImageVersionComparer.ChannelRelease);
        dto.UpdateAvailable.ShouldBeTrue();
        dto.NewerTags.ShouldBe(["1.3.0", "1.2.4"]);
        dto.Source.ShouldBe("ghcr.io/ildoc/pickarr");
        dto.CurrentVersion.ShouldBe("1.2.3");
    }

    [Fact]
    public async Task CheckAsync_development_includes_versioned_tags()
    {
        var source = Substitute.For<IImageTagSource>();
        source.ListTagsAsync(Arg.Any<CancellationToken>())
            .Returns(["1.2.3.5", "1.2.4", "edge", "1.2.3.4"]);

        var sut = CreateSut(source);
        var dto = await sut.CheckAsync("1.2.3.4");

        dto.Channel.ShouldBe(ImageVersionComparer.ChannelDevelopment);
        dto.NewerTags.ShouldBe(["1.2.4", "1.2.3.5"]);
        dto.UpdateAvailable.ShouldBeTrue();
    }

    [Fact]
    public async Task CheckAsync_fail_soft_when_tag_source_returns_empty()
    {
        var source = Substitute.For<IImageTagSource>();
        source.ListTagsAsync(Arg.Any<CancellationToken>()).Returns([]);

        var sut = CreateSut(source);
        var dto = await sut.CheckAsync("1.0.0");

        dto.UpdateAvailable.ShouldBeFalse();
        dto.NewerTags.ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckAsync_caches_tag_list()
    {
        var source = Substitute.For<IImageTagSource>();
        source.ListTagsAsync(Arg.Any<CancellationToken>()).Returns(["1.1.0"]);

        var sut = CreateSut(source);
        await sut.CheckAsync("1.0.0");
        await sut.CheckAsync("1.0.0");

        await source.Received(1).ListTagsAsync(Arg.Any<CancellationToken>());
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
