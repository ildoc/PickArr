import { useTranslation } from 'react-i18next'
import { arrStateLabelKey } from '../../lib/format'

const stateClass: Record<string, string> = {
  notInLibrary: 'bg-white/10 text-[var(--muted)]',
  missing: 'bg-amber-500/20 text-amber-200',
  downloading: 'bg-sky-500/20 text-sky-200',
  downloaded: 'bg-emerald-500/20 text-emerald-200',
  partial: 'bg-orange-500/20 text-orange-200',
}

export function ArrStatusBadge({
  state,
  progress,
}: {
  state: string
  progress?: number | null
}) {
  const { t } = useTranslation()
  const label = t(arrStateLabelKey(state))
  const suffix =
    state === 'downloading' && progress != null ? ` ${Math.round(progress)}%` : ''

  return (
    <span
      className={`inline-flex rounded px-1.5 py-0.5 text-[10px] font-medium uppercase tracking-wide ${stateClass[state] ?? stateClass.notInLibrary}`}
    >
      {label}
      {suffix}
    </span>
  )
}
