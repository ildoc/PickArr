import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router-dom'
import { fetchRecommendations } from '../../api/recommendations'
import { fetchSettings } from '../../api/settings'
import { TitleCard } from '../catalog/TitleCard'

const CatalogForYouLimit = 25

export function CatalogForYouRow({ sourceId }: { sourceId: number }) {
  const { t } = useTranslation()
  const settings = useQuery({ queryKey: ['settings'], queryFn: fetchSettings })
  const recoEnabled = settings.isSuccess && settings.data.reco.enabled !== false

  const reco = useQuery({
    queryKey: ['recommendations', 'source', sourceId, 'catalog-row'],
    enabled: recoEnabled,
    queryFn: () =>
      fetchRecommendations({
        scope: 'source',
        sourceId,
        page: 1,
        pageSize: CatalogForYouLimit,
      }),
  })

  if (!settings.isSuccess || !recoEnabled) {
    return null
  }

  if (reco.isLoading) {
    return (
      <section
        className="space-y-3 border-b border-white/10 pb-6"
        aria-busy="true"
      >
        <h2 className="text-sm font-medium uppercase tracking-wide text-[var(--muted)]">
          {t('reco.catalogRowHeading')}
        </h2>
        <p className="text-sm text-[var(--muted)]">{t('reco.catalogRowLoading')}</p>
      </section>
    )
  }

  if (reco.isError || !reco.data || reco.data.items.length === 0) {
    return null
  }

  return (
    <section className="space-y-3 border-b border-white/10 pb-6">
      <div className="flex flex-wrap items-baseline justify-between gap-2">
        <h2 className="text-sm font-medium uppercase tracking-wide text-[var(--muted)]">
          {t('reco.catalogRowHeading')}
        </h2>
        <Link
          to="/for-you"
          className="text-xs text-[var(--muted)] underline-offset-2 hover:text-[var(--text)] hover:underline"
        >
          {t('reco.catalogRowMore')}
        </Link>
      </div>
      <ul className="flex gap-3 overflow-x-auto pb-1">
        {reco.data.items.map((title) => (
          <li key={title.id} className="w-[9.5rem] shrink-0 sm:w-44">
            <TitleCard title={title} dismissOnRate />
          </li>
        ))}
      </ul>
    </section>
  )
}
