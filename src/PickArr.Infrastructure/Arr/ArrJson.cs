using System.Text.Json;
using System.Text.Json.Serialization;

namespace Pickarr.Infrastructure.Arr;

internal static class ArrJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };
}
