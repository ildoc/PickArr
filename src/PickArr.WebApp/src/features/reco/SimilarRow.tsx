import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { useNavigate, useLocation, useSearchParams } from 'react-router-dom'
import { fetchSimilarTitles } from '../../api/recommendations'
import { fetchSettings } from '../../api/settings'

export function SimilarRow({
  titleId,
  sourceId,
}: {
  titleId: number
  sourceId: number | null
}) {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const { pathname } = useLocation()
  const [params] = useSearchParams()
  const settings = useQuery({ queryKey: ['settings'], queryFn: fetchSettings })
  const recoEnabled = settings.data?.reco.enabled !== false

  const similar = useQuery({
    queryKey: ['similar', titleId, sourceId],
    queryFn: () => fetchSimilarTitles(titleId, sourceId),
    enabled: recoEnabled,
  })

  if (!recoEnabled) return null
  if (similar.isLoading) {
    return <p className="text-sm text-[var(--muted)]">{t('reco.similarLoading')}</p>
  }
  if (similar.isError || !similar.data || similar.data.items.length === 0) {
    return null
  }

  const openTitle = (id: number) => {
    const next = new URLSearchParams(params)
    next.set('title', String(id))
    navigate({ pathname, search: next.toString() })
  }

  return (
    <section className="space-y-3">
      <h3 className="text-sm font-medium uppercase tracking-wide text-[var(--muted)]">
        {t('reco.similarHeading')}
      </h3>
      <ul className="flex gap-3 overflow-x-auto pb-1">
        {similar.data.items.map((item) => (
          <li key={item.id} className="w-28 shrink-0">
            <button
              type="button"
              onClick={() => openTitle(item.id)}
              className="w-full text-left"
            >
              {item.posterUrl ? (
                <img
                  src={item.posterUrl}
                  alt=""
                  className="aspect-[2/3] w-full rounded-md object-cover"
                />
              ) : (
                <div className="aspect-[2/3] w-full rounded-md bg-white/5" />
              )}
              <p className="mt-1 line-clamp-2 text-xs leading-snug">{item.name}</p>
              {item.places && item.places.length > 0 && (
                <p className="mt-0.5 text-[10px] text-[var(--muted)]">
                  {item.places.map((p) => `${p.providerName} ${p.countryCode}`).join(' · ')}
                </p>
              )}
            </button>
          </li>
        ))}
      </ul>
    </section>
  )
}
