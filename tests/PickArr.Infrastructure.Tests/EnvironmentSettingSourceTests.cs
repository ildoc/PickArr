using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PickArr.Core.Abstractions;
using PickArr.Core.Settings;
using PickArr.Infrastructure.Settings;
using Shouldly;

namespace PickArr.Infrastructure.Tests;

public sealed class EnvironmentSettingSourceTests
{
    [Fact]
    public void Catalog_covers_every_settings_key_except_import_state()
    {
        var keys = typeof(SettingsKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToList();

        keys.ShouldContain(SettingsKeys.CatalogInitialImportStarted);
        foreach (var key in keys.Where(key => key != SettingsKeys.CatalogInitialImportStarted))
        {
            SettingEnvironmentCatalog.CanonicalKeys.ShouldContain(key);
        }

        SettingEnvironmentCatalog.CanonicalKeys.ShouldNotContain(SettingsKeys.CatalogInitialImportStarted);
    }

    [Fact]
    public void Declared_values_win_and_are_json()
    {
        var source = Create(new Dictionary<string, string?>
        {
            ["PickArr:Settings:watchmode:apiKey"] = "secret-key",
            ["PickArr:Settings:watchmode:quota"] = "1200",
            ["PickArr:Settings:reco:enabled"] = "false",
            ["PickArr:Settings:reco:weights:dislike"] = "-1.5",
            ["PickArr:Settings:sonarr:url"] = "http://sonarr:8989/",
            ["PickArr:Settings:sonarr:defaults:qualityProfileId"] = "4",
            ["PickArr:Settings:sonarr:defaults:rootFolderPath"] = "/tv",
        });

        source.TryGet(SettingsKeys.WatchmodeApiKey, out var apiKey).ShouldBeTrue();
        apiKey.ShouldBe("\"secret-key\"");
        source.TryGet(SettingsKeys.WatchmodeQuota, out var quota).ShouldBeTrue();
        quota.ShouldBe("1200");
        source.TryGet(SettingsKeys.RecoEnabled, out var enabled).ShouldBeTrue();
        enabled.ShouldBe("false");
        source.TryGet(SettingsKeys.RecoWeightDislike, out var dislike).ShouldBeTrue();
        dislike.ShouldBe("-1.5");
        source.TryGet(SettingsKeys.SonarrUrl, out var url).ShouldBeTrue();
        url.ShouldBe("\"http://sonarr:8989\"");
        source.TryGet(SettingsKeys.SonarrDefaults, out var defaults).ShouldBeTrue();
        defaults.ShouldContain("\"qualityProfileId\":4");
        defaults.ShouldContain("\"rootFolderPath\":\"/tv\"");
    }

    [Fact]
    public void Defaults_accept_a_json_object()
    {
        var source = Create(new Dictionary<string, string?>
        {
            ["PickArr:Settings:radarr:defaults"] = "{\"qualityProfileId\":2,\"rootFolderPath\":\"/movies\"}",
        });

        source.TryGet(SettingsKeys.RadarrDefaults, out var defaults).ShouldBeTrue();
        defaults.ShouldContain("\"qualityProfileId\":2");
        defaults.ShouldContain("\"rootFolderPath\":\"/movies\"");
    }

    [Fact]
    public void Blank_values_are_not_declared()
    {
        var source = Create(new Dictionary<string, string?>
        {
            ["PickArr:Settings:watchmode:apiKey"] = "  ",
            ["PickArr:Settings:ui:language"] = "en",
        });

        source.TryGet(SettingsKeys.WatchmodeApiKey, out _).ShouldBeFalse();
        source.TryGet(SettingsKeys.UiLanguage, out var language).ShouldBeTrue();
        language.ShouldBe("\"en\"");
    }

    [Fact]
    public void Invalid_number_fails_at_startup()
    {
        var error = Should.Throw<InvalidOperationException>(() => Create(new Dictionary<string, string?>
        {
            ["PickArr:Settings:watchmode:quota"] = "many",
        }));

        error.Message.ShouldContain(SettingsKeys.WatchmodeQuota);
        error.Message.ShouldNotContain("many");
    }

    [Fact]
    public void Unknown_key_fails_at_startup()
    {
        var error = Should.Throw<InvalidOperationException>(() => Create(new Dictionary<string, string?>
        {
            ["PickArr:Settings:catalog:initialImportStarted"] = "true",
        }));

        error.Message.ShouldContain("catalog.initialImportStarted");
    }

    [Fact]
    public async Task Store_reads_the_declaration_and_ignores_writes()
    {
        var source = Create(new Dictionary<string, string?>
        {
            ["PickArr:Settings:watchmode:plan"] = "paid",
        });
        var inner = Substitute.For<ISettingsStore>();
        inner.IsSetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        var store = new EnvironmentSettingsStore(inner, source);

        (await store.GetRawAsync(SettingsKeys.WatchmodePlan)).ShouldBe("\"paid\"");
        (await store.IsSetAsync(SettingsKeys.WatchmodeApiKey)).ShouldBeFalse();
        await store.SetAsync(SettingsKeys.WatchmodePlan, "\"free\"", false);
        await store.DeleteAsync(SettingsKeys.WatchmodePlan);
        await store.SetAsync(SettingsKeys.UiLanguage, "\"it\"", false);

        await inner.DidNotReceive().GetRawAsync(SettingsKeys.WatchmodePlan, Arg.Any<CancellationToken>());
        await inner.DidNotReceive().SetAsync(
            SettingsKeys.WatchmodePlan,
            Arg.Any<string>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
        await inner.DidNotReceive().DeleteAsync(SettingsKeys.WatchmodePlan, Arg.Any<CancellationToken>());
        await inner.Received(1).SetAsync(
            SettingsKeys.UiLanguage,
            "\"it\"",
            false,
            Arg.Any<CancellationToken>());
    }

    private static EnvironmentSettingSource Create(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        using var loggerFactory = LoggerFactory.Create(_ => { });
        return new EnvironmentSettingSource(configuration, loggerFactory.CreateLogger<EnvironmentSettingSource>());
    }
}
