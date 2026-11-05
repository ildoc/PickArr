using NSubstitute;
using PickArr.Core.Abstractions;
using PickArr.Core.Settings;
using Shouldly;

namespace PickArr.Core.Tests;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task GetAsync_exposes_secret_only_as_isSet()
    {
        var store = Substitute.For<ISettingsStore>();
        store.IsSetAsync(SettingsKeys.WatchmodeApiKey, Arg.Any<CancellationToken>()).Returns(true);
        store.GetRawAsync(SettingsKeys.WatchmodePlan, Arg.Any<CancellationToken>()).Returns("\"free\"");
        store.GetRawAsync(SettingsKeys.WatchmodeQuota, Arg.Any<CancellationToken>()).Returns("2500");
        store.GetRawAsync(SettingsKeys.WatchmodeReservePercent, Arg.Any<CancellationToken>()).Returns("10");
        store.GetRawAsync(SettingsKeys.WatchmodeMaxCreditsPerRun, Arg.Any<CancellationToken>()).Returns("80");
        store.GetRawAsync(SettingsKeys.UiLanguage, Arg.Any<CancellationToken>()).Returns("\"en\"");
        store.GetRawAsync("sonarr.syncIntervalMinutes", Arg.Any<CancellationToken>()).Returns("15");
        store.GetRawAsync("radarr.syncIntervalMinutes", Arg.Any<CancellationToken>()).Returns("15");

        var sut = new SettingsService(store);
        var result = await sut.GetAsync();

        result.Watchmode.ApiKey.IsSet.ShouldBeTrue();
        result.Watchmode.Plan.ShouldBe("free");
    }

    [Fact]
    public async Task UpdateAsync_writes_secret_without_returning_it()
    {
        var store = Substitute.For<ISettingsStore>();
        var sut = new SettingsService(store);

        await sut.UpdateAsync(new UpdateSettingsRequest
        {
            Watchmode = new WatchmodeUpdateDto { ApiKey = "secret-key" },
        });

        await store.Received(1).SetAsync(
            SettingsKeys.WatchmodeApiKey,
            Arg.Is<string>(v => v.Contains("secret-key")),
            true,
            Arg.Any<CancellationToken>());
    }
}
