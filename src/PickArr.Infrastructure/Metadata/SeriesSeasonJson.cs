using System.Text.Json;
using PickArr.Core.Metadata;

namespace PickArr.Infrastructure.Metadata;

internal static class SeriesSeasonJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public static List<TmdbSeason> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<TmdbSeason>>(json, Options) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string Write(IReadOnlyList<TmdbSeason> seasons) =>
        JsonSerializer.Serialize(seasons, Options);

    public static List<TmdbSeason> MergeSummaries(string? existingJson, IReadOnlyList<TmdbSeason> incoming)
    {
        var episodes = Read(existingJson)
            .Where(season => season.Episodes is { Count: > 0 })
            .ToDictionary(season => season.Number, season => season.Episodes!);

        return incoming
            .Select(season => episodes.TryGetValue(season.Number, out var cached)
                ? season with { Episodes = cached }
                : season)
            .ToList();
    }

    public static List<TmdbSeason> WithEpisodes(
        IReadOnlyList<TmdbSeason> seasons,
        int seasonNumber,
        IReadOnlyList<TmdbEpisode> episodes)
    {
        var ordered = episodes.OrderBy(episode => episode.Number).ToList();
        var list = seasons.ToList();
        var index = list.FindIndex(season => season.Number == seasonNumber);
        if (index < 0)
        {
            list.Add(new TmdbSeason(
                seasonNumber,
                seasonNumber == 0 ? "Specials" : $"Season {seasonNumber}",
                ordered.Count,
                AirDate: null,
                Overview: null,
                Episodes: ordered));
            return list;
        }

        var current = list[index];
        list[index] = current with
        {
            Episodes = ordered,
            EpisodeCount = Math.Max(current.EpisodeCount, ordered.Count),
        };
        return list;
    }
}
