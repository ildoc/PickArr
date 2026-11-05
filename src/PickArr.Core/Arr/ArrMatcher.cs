using PickArr.Core.Domain;
using PickArr.Core.Domain.Entities;

namespace PickArr.Core.Arr;

public static class ArrMatcher
{
    public static int? MatchTitleId(ArrKind kind, ArrItemSnapshot item, IReadOnlyList<Title> titles)
    {
        if (kind == ArrKind.Radarr)
        {
            if (item.TmdbId is { } tmdb)
            {
                var byTmdb = titles.FirstOrDefault(t =>
                    t.TmdbId == tmdb && string.Equals(t.TmdbType, "movie", StringComparison.OrdinalIgnoreCase));
                if (byTmdb is not null)
                {
                    return byTmdb.Id;
                }
            }

            if (!string.IsNullOrWhiteSpace(item.ImdbId))
            {
                return titles.FirstOrDefault(t =>
                    string.Equals(t.ImdbId, item.ImdbId, StringComparison.OrdinalIgnoreCase))?.Id;
            }

            return null;
        }

        if (item.TvdbId is { } tvdb)
        {
            var byTvdb = titles.FirstOrDefault(t => t.TvdbId == tvdb);
            if (byTvdb is not null)
            {
                return byTvdb.Id;
            }
        }

        if (item.TmdbId is { } seriesTmdb)
        {
            var byTmdb = titles.FirstOrDefault(t =>
                t.TmdbId == seriesTmdb && string.Equals(t.TmdbType, "tv", StringComparison.OrdinalIgnoreCase));
            if (byTmdb is not null)
            {
                return byTmdb.Id;
            }
        }

        if (!string.IsNullOrWhiteSpace(item.ImdbId))
        {
            return titles.FirstOrDefault(t =>
                string.Equals(t.ImdbId, item.ImdbId, StringComparison.OrdinalIgnoreCase))?.Id;
        }

        return null;
    }
}
