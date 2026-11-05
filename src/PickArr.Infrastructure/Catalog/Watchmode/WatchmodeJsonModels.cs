using System.Text.Json.Serialization;

namespace PickArr.Infrastructure.Catalog.Watchmode;

internal sealed class WatchmodeListTitlesResponse
{
    [JsonPropertyName("titles")]
    public List<WatchmodeTitleItem> Titles { get; set; } = [];

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("total_results")]
    public int TotalResults { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }
}

internal sealed class WatchmodeTitleItem
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("year")]
    public int? Year { get; set; }

    [JsonPropertyName("imdb_id")]
    public string? ImdbId { get; set; }

    [JsonPropertyName("tmdb_id")]
    public int? TmdbId { get; set; }

    [JsonPropertyName("tmdb_type")]
    public string? TmdbType { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

internal sealed class WatchmodeSourceItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("regions")]
    public List<string> Regions { get; set; } = [];
}

internal sealed class WatchmodeRegionItem
{
    [JsonPropertyName("country")]
    public string Country { get; set; } = "";

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("supported")]
    public bool Supported { get; set; } = true;
}

internal sealed class WatchmodeLeavingResponse
{
    [JsonPropertyName("leaving_dates")]
    public List<WatchmodeLeavingItem> LeavingDates { get; set; } = [];

    [JsonPropertyName("page")]
    public int Page { get; set; }

    [JsonPropertyName("total_pages")]
    public int TotalPages { get; set; }

    [JsonPropertyName("total_results")]
    public int TotalResults { get; set; }
}

internal sealed class WatchmodeLeavingItem
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = "";

    [JsonPropertyName("title_type")]
    public string? TitleType { get; set; }

    [JsonPropertyName("region")]
    public string Region { get; set; } = "";

    [JsonPropertyName("leaving_date")]
    public string LeavingDate { get; set; } = "";

    [JsonPropertyName("provider_id")]
    public int? ProviderId { get; set; }

    [JsonPropertyName("season")]
    public int? Season { get; set; }
}

internal sealed class WatchmodeErrorBody
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("statusCode")]
    public int? StatusCode { get; set; }

    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("statusMessage")]
    public string? StatusMessage { get; set; }
}

internal sealed class WatchmodeChangesResponse
{
    [JsonPropertyName("titles")]
    public List<WatchmodeTitleItem>? Titles { get; set; }

    [JsonPropertyName("changes")]
    public List<WatchmodeTitleItem>? Changes { get; set; }
}
