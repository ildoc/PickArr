using Serilog.Events;

namespace Pickarr.Api.Logging;

public static class LogLevelResolver
{
    public const string EnvironmentVariableName = "LOG_LEVEL";
    public const LogEventLevel DefaultLevel = LogEventLevel.Debug;

    /// <summary>
    /// Resolves the process log level from <c>LOG_LEVEL</c>, then legacy
    /// <c>Logging:Level</c> / <c>Serilog:MinimumLevel:Default</c>. Empty or unknown → Debug.
    /// </summary>
    public static LogEventLevel Resolve(IConfiguration configuration)
    {
        var raw = FirstNonEmpty(
            configuration[EnvironmentVariableName],
            configuration["Logging:Level"],
            configuration["Serilog:MinimumLevel:Default"]);
        return Parse(raw);
    }

    public static LogEventLevel Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultLevel;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "verbose" or "trace" => LogEventLevel.Verbose,
            "debug" => LogEventLevel.Debug,
            "information" or "info" => LogEventLevel.Information,
            "warning" or "warn" => LogEventLevel.Warning,
            "error" => LogEventLevel.Error,
            "fatal" or "critical" => LogEventLevel.Fatal,
            _ => Enum.TryParse<LogEventLevel>(value.Trim(), ignoreCase: true, out var level)
                ? level
                : DefaultLevel,
        };
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
