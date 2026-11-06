using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PickArr.Core.Settings;

namespace PickArr.Infrastructure.Settings;

/// <summary>
/// Values declared under <c>PickArr:Settings</c> (environment <c>PickArr__Settings__…</c>).
/// A set variable is the effective value for the process; the database is not consulted for that key.
/// </summary>
internal sealed class EnvironmentSettingSource
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<string, string> _jsonByKey;

    public EnvironmentSettingSource(IConfiguration configuration, ILogger<EnvironmentSettingSource> logger)
    {
        _jsonByKey = Read(configuration);
        if (_jsonByKey.Count > 0)
        {
            logger.LogInformation(
                "Settings declared by configuration: {Keys}",
                string.Join(", ", _jsonByKey.Keys.OrderBy(key => key, StringComparer.Ordinal)));
        }
    }

    public bool TryGet(string key, out string json) => _jsonByKey.TryGetValue(key, out json!);

    private static Dictionary<string, string> Read(IConfiguration configuration)
    {
        var leaves = new List<(string Path, string Value)>();
        foreach (var child in configuration.GetSection(SettingEnvironmentCatalog.SectionName).GetChildren())
        {
            Collect(child, child.Key, leaves);
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var defaultsFields = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);

        foreach (var (path, value) in leaves.OrderBy(leaf => leaf.Path, StringComparer.OrdinalIgnoreCase))
        {
            var dotted = path.Replace(':', '.');
            if (SettingEnvironmentCatalog.TryGet(dotted, out var canonical, out var kind))
            {
                if (kind == SettingValueKind.Defaults)
                {
                    if (defaultsFields.ContainsKey(canonical))
                    {
                        throw BothForms(canonical);
                    }

                    result[canonical] = EncodeDefaultsJson(canonical, value);
                }
                else
                {
                    result[canonical] = EncodeScalar(canonical, kind, value);
                }

                continue;
            }

            if (SettingEnvironmentCatalog.TryMatchDefaultsField(dotted, out var defaultsKey, out var field))
            {
                if (result.ContainsKey(defaultsKey))
                {
                    throw BothForms(defaultsKey);
                }

                if (!defaultsFields.TryGetValue(defaultsKey, out var fields))
                {
                    fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    defaultsFields[defaultsKey] = fields;
                }

                fields[field] = value;
                continue;
            }

            throw new InvalidOperationException(
                $"Unknown setting '{dotted}'. Declare it as PickArr__Settings__ plus the settings key, with each dot replaced by '__'.");
        }

        foreach (var (key, fields) in defaultsFields)
        {
            result[key] = EncodeDefaultsFields(key, fields);
        }

        return result;
    }

    private static void Collect(IConfigurationSection section, string path, List<(string Path, string Value)> leaves)
    {
        var children = section.GetChildren().ToList();
        if (children.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(section.Value))
            {
                leaves.Add((path, section.Value));
            }

            return;
        }

        foreach (var child in children)
        {
            Collect(child, path + ":" + child.Key, leaves);
        }
    }

    private static string EncodeScalar(string key, SettingValueKind kind, string value)
    {
        var trimmed = value.Trim();
        switch (kind)
        {
            case SettingValueKind.String:
                return JsonSerializer.Serialize(trimmed);
            case SettingValueKind.Url:
                var url = trimmed.TrimEnd('/');
                if (url.Length == 0)
                {
                    throw new InvalidOperationException($"Setting '{key}' must be a non-empty URL.");
                }

                return JsonSerializer.Serialize(url);
            case SettingValueKind.Int32:
                if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integer))
                {
                    throw new InvalidOperationException($"Setting '{key}' must be an integer.");
                }

                return JsonSerializer.Serialize(integer);
            case SettingValueKind.Double:
                if (!double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    throw new InvalidOperationException($"Setting '{key}' must be a number.");
                }

                return JsonSerializer.Serialize(number);
            case SettingValueKind.Boolean:
                if (!bool.TryParse(trimmed, out var flag))
                {
                    throw new InvalidOperationException($"Setting '{key}' must be true or false.");
                }

                return JsonSerializer.Serialize(flag);
            default:
                throw new InvalidOperationException($"Setting '{key}' must be a single value.");
        }
    }

    private static string EncodeDefaultsJson(string key, string value)
    {
        ArrDefaultsDto? dto;
        try
        {
            using var doc = JsonDocument.Parse(value);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"Setting '{key}' must be a JSON object.");
            }

            dto = JsonSerializer.Deserialize<ArrDefaultsDto>(value, JsonOptions);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException($"Setting '{key}' must be a JSON object.");
        }

        return JsonSerializer.Serialize(dto ?? new ArrDefaultsDto(), JsonOptions);
    }

    private static string EncodeDefaultsFields(string key, Dictionary<string, string> fields)
    {
        int? profile = null;
        string? root = null;
        foreach (var (name, raw) in fields)
        {
            if (name.Equals("qualityProfileId", StringComparison.OrdinalIgnoreCase))
            {
                if (!int.TryParse(raw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                {
                    throw new InvalidOperationException($"Setting '{key}.qualityProfileId' must be an integer.");
                }

                profile = id;
            }
            else if (name.Equals("rootFolderPath", StringComparison.OrdinalIgnoreCase))
            {
                var trimmed = raw.Trim();
                if (trimmed.Length > 0)
                {
                    root = trimmed;
                }
            }
            else
            {
                throw new InvalidOperationException($"Setting '{key}' does not have a field '{name}'.");
            }
        }

        if (profile is null && root is null)
        {
            throw new InvalidOperationException($"Setting '{key}' has no values.");
        }

        return JsonSerializer.Serialize(
            new ArrDefaultsDto { QualityProfileId = profile, RootFolderPath = root },
            JsonOptions);
    }

    private static InvalidOperationException BothForms(string key) =>
        new($"Setting '{key}' must be either a JSON object or nested fields, not both.");
}
