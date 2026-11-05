using PickArr.Core.Domain;

namespace PickArr.Core.Arr;

public static class ArrStatusCalculator
{
    public static ArrState ForMovie(bool hasFile, bool inQueue) =>
        inQueue ? ArrState.Downloading :
        hasFile ? ArrState.Downloaded :
        ArrState.Missing;

    public static ArrState ForSeason(ArrSeasonSnapshot season, bool inQueue) =>
        inQueue ? ArrState.Downloading :
        season.EpisodeCount == 0 ? ArrState.Missing :
        season.EpisodeFileCount >= season.EpisodeCount ? ArrState.Downloaded :
        season.EpisodeFileCount > 0 ? ArrState.Partial :
        ArrState.Missing;

    public static ArrState ForSeries(IReadOnlyList<(ArrSeasonSnapshot Season, ArrState State)> seasons, bool anyInQueue)
    {
        if (anyInQueue)
        {
            return ArrState.Downloading;
        }

        var monitored = seasons
            .Where(s => s.Season.Monitored && s.Season.EpisodeCount > 0)
            .ToList();
        if (monitored.Count == 0)
        {
            return ArrState.Missing;
        }

        if (monitored.All(s => s.State == ArrState.Downloaded))
        {
            return ArrState.Downloaded;
        }

        if (monitored.Any(s => s.Season.EpisodeFileCount > 0))
        {
            return ArrState.Partial;
        }

        return ArrState.Missing;
    }

    public static double? QueueProgress(IEnumerable<ArrQueueEntry> entries)
    {
        var list = entries.Where(e => e.Size > 0).ToList();
        if (list.Count == 0)
        {
            return null;
        }

        var total = list.Sum(e => e.Size);
        var left = list.Sum(e => e.SizeLeft);
        if (total <= 0)
        {
            return null;
        }

        return Math.Clamp((1.0 - (left / total)) * 100.0, 0, 100);
    }
}
