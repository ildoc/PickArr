import { useTranslation } from 'react-i18next'

const VIEWS = ['all', 'new', 'leaving', 'removed'] as const

export function QuickFilters({
  value,
  onChange,
}: {
  value: string
  onChange: (view: string) => void
}) {
  const { t } = useTranslation()

  return (
    <div className="flex flex-wrap gap-2">
      {VIEWS.map((view) => {
        const active = value === view
        return (
          <button
            key={view}
            type="button"
            onClick={() => onChange(view)}
            className={`rounded-full px-3 py-1 text-sm transition ${
              active
                ? 'bg-[var(--accent)] text-white'
                : 'bg-white/5 text-[var(--muted)] hover:bg-white/10 hover:text-[var(--text)]'
            }`}
          >
            {t(`views.${view}`)}
          </button>
        )
      })}
    </div>
  )
}
