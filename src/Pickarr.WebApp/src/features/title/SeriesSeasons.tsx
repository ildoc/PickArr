import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fetchSeasonEpisodes, fetchSeriesSeasons } from '../../api/catalog'
import { formatDate } from '../../lib/format'

export function SeriesSeasons({
  titleId,
  sourceId,
}: {
  titleId: number
  sourceId: number | null
}) {
  const { t, i18n } = useTranslation()
  const [openSeason, setOpenSeason] = useState<number | null>(null)
  const seasons = useQuery({
    queryKey: ['title-seasons', titleId, sourceId],
    queryFn: () => fetchSeriesSeasons(titleId, sourceId ?? undefined),
  })

  return (
    <section>
      <h3 className="mb-2 text-sm font-medium uppercase tracking-wide text-[var(--muted)]">
        {t('title.seasonList')}
      </h3>
      {seasons.isLoading && <p className="text-sm text-[var(--muted)]">{t('title.seasonsLoading')}</p>}
      {seasons.isError && <p className="text-sm text-[var(--muted)]">{t('title.seasonsError')}</p>}
      {seasons.data && seasons.data.length === 0 && (
        <p className="text-sm text-[var(--muted)]">{t('title.seasonsEmpty')}</p>
      )}
      {seasons.data && seasons.data.length > 0 && (
        <ul className="divide-y divide-white/10 rounded-lg border border-white/10">
          {seasons.data.map((season) => {
            const open = openSeason === season.number
            return (
              <li key={season.number}>
                <button
                  type="button"
                  className="flex w-full items-center justify-between gap-3 px-3 py-2 text-left text-sm hover:bg-white/5"
                  aria-expanded={open}
                  onClick={() => setOpenSeason(open ? null : season.number)}
                >
                  <span>
                    {season.number === 0 ? t('title.specials') : `${t('title.season')} ${season.number}`}
                    {season.name && season.number !== 0 ? ` · ${season.name}` : ''}
                  </span>
                  <span className="text-xs text-[var(--muted)]">
                    {t('title.episodeCount', { count: season.episodeCount })}
                    {season.airDate ? ` · ${formatDate(season.airDate, i18n.language)}` : ''}
                  </span>
                </button>
                {open && <EpisodeList titleId={titleId} sourceId={sourceId} seasonNumber={season.number} />}
              </li>
            )
          })}
        </ul>
      )}
    </section>
  )
}

function EpisodeList({
  titleId,
  sourceId,
  seasonNumber,
}: {
  titleId: number
  sourceId: number | null
  seasonNumber: number
}) {
  const { t, i18n } = useTranslation()
  const episodes = useQuery({
    queryKey: ['title-episodes', titleId, sourceId, seasonNumber],
    queryFn: () => fetchSeasonEpisodes(titleId, seasonNumber, sourceId ?? undefined),
  })

  if (episodes.isLoading) {
    return <p className="px-3 pb-3 text-sm text-[var(--muted)]">{t('title.episodesLoading')}</p>
  }
  if (episodes.isError) {
    return <p className="px-3 pb-3 text-sm text-[var(--muted)]">{t('title.episodesError')}</p>
  }
  if (!episodes.data || episodes.data.length === 0) {
    return <p className="px-3 pb-3 text-sm text-[var(--muted)]">{t('title.episodesEmpty')}</p>
  }

  return (
    <ol className="space-y-2 px-3 pb-3">
      {episodes.data.map((episode) => (
        <li key={episode.number} className="text-sm">
          <p>
            <span className="text-[var(--muted)]">{episode.number}.</span> {episode.name}
            {episode.airDate && (
              <span className="text-[var(--muted)]"> · {formatDate(episode.airDate, i18n.language)}</span>
            )}
          </p>
          {episode.overview && (
            <p className="mt-0.5 text-xs leading-relaxed text-[var(--muted)]">{episode.overview}</p>
          )}
        </li>
      ))}
    </ol>
  )
}
