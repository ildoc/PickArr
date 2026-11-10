using Microsoft.Extensions.Configuration;
using Pickarr.Api.Logging;
using Serilog.Events;
using Shouldly;

namespace Pickarr.Integration.Tests;

public sealed class LogLevelResolverTests
{
    [Theory]
    [InlineData(null, LogEventLevel.Debug)]
    [InlineData("", LogEventLevel.Debug)]
    [InlineData("   ", LogEventLevel.Debug)]
    [InlineData("debug", LogEventLevel.Debug)]
    [InlineData("DEBUG", LogEventLevel.Debug)]
    [InlineData("Information", LogEventLevel.Information)]
    [InlineData("info", LogEventLevel.Information)]
    [InlineData("warn", LogEventLevel.Warning)]
    [InlineData("Warning", LogEventLevel.Warning)]
    [InlineData("error", LogEventLevel.Error)]
    [InlineData("fatal", LogEventLevel.Fatal)]
    [InlineData("critical", LogEventLevel.Fatal)]
    [InlineData("verbose", LogEventLevel.Verbose)]
    [InlineData("trace", LogEventLevel.Verbose)]
    [InlineData("not-a-level", LogEventLevel.Debug)]
    public void Parse_maps_known_aliases_and_defaults_to_debug(string? value, LogEventLevel expected)
    {
        LogLevelResolver.Parse(value).ShouldBe(expected);
    }

    [Fact]
    public void Resolve_prefers_LOG_LEVEL_over_legacy_keys()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LOG_LEVEL"] = "Warning",
                ["Logging:Level"] = "Error",
                ["Serilog:MinimumLevel:Default"] = "Information",
            })
            .Build();

        LogLevelResolver.Resolve(configuration).ShouldBe(LogEventLevel.Warning);
    }

    [Fact]
    public void Resolve_falls_back_to_legacy_Logging_Level()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Logging:Level"] = "Error",
                ["Serilog:MinimumLevel:Default"] = "Information",
            })
            .Build();

        LogLevelResolver.Resolve(configuration).ShouldBe(LogEventLevel.Error);
    }

    [Fact]
    public void Resolve_defaults_to_debug_when_unset()
    {
        var configuration = new ConfigurationBuilder().Build();
        LogLevelResolver.Resolve(configuration).ShouldBe(LogEventLevel.Debug);
    }
}
