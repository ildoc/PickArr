import { useTranslation } from 'react-i18next'
import type { RecoReason } from '../../api/types'

export function ReasonChips({ reasons }: { reasons: RecoReason[] }) {
  const { t } = useTranslation()
  if (reasons.length === 0) return null

  return (
    <ul className="mt-1 flex flex-wrap gap-1">
      {reasons.map((reason, index) => {
        const label = labelFor(reason, t)
        if (!label) return null
        return (
          <li
            key={`${reason.kind}-${index}`}
            className="rounded bg-white/5 px-1.5 py-0.5 text-[10px] text-[var(--muted)]"
          >
            {label}
          </li>
        )
      })}
    </ul>
  )
}

function labelFor(reason: RecoReason, t: (key: string, opts?: Record<string, unknown>) => string) {
  switch (reason.kind) {
    case 'similarTo':
      return t('reco.reason.similarTo', { name: reason.name ?? '' })
    case 'terms':
      return t('reco.reason.terms', { terms: (reason.terms ?? []).join(', ') })
    case 'popular':
      return t('reco.reason.popular')
    case 'explore':
      return t('reco.reason.explore')
    default:
      return null
  }
}
