import { useTranslation } from 'react-i18next'
import { ArrStatusBadge } from './ArrStatusBadge'

export type SeasonRow = {
  number: number
  monitored: boolean
  state: string
  episodeFileCount: number
  episodeCount: number
  totalEpisodeCount: number
}

export function SeasonTable({ seasons }: { seasons: SeasonRow[] }) {
  const { t } = useTranslation()
  if (seasons.length === 0) return null

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-left text-sm">
        <thead className="text-[var(--muted)]">
          <tr>
            <th className="py-1 pr-3 font-medium">{t('title.season')}</th>
            <th className="py-1 pr-3 font-medium">{t('title.monitored')}</th>
            <th className="py-1 pr-3 font-medium">{t('title.episodes')}</th>
            <th className="py-1 font-medium">{t('title.library')}</th>
          </tr>
        </thead>
        <tbody>
          {seasons.map((s) => (
            <tr key={s.number} className="border-t border-white/5">
              <td className="py-1.5 pr-3">{s.number === 0 ? t('title.specials') : s.number}</td>
              <td className="py-1.5 pr-3">{s.monitored ? '✓' : '—'}</td>
              <td className="py-1.5 pr-3">
                {s.episodeFileCount}/{s.episodeCount}
              </td>
              <td className="py-1.5">
                <ArrStatusBadge state={s.state} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
