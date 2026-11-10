import { Link } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import type { RecoStatus } from '../../api/types'

export function LearningProgress({ status }: { status: RecoStatus }) {
  const { t } = useTranslation()
  const personalized = status.targetN > 0 && status.effectiveN >= status.targetN
  if (personalized) return null

  const ratio = status.targetN > 0 ? Math.min(1, status.effectiveN / status.targetN) : 0

  return (
    <div className="rounded-xl border border-white/10 bg-[var(--surface-2)] px-4 py-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <p className="text-sm font-medium text-[var(--text)]">
            {t(`reco.stage.${status.stage}`, { defaultValue: status.stage })}
          </p>
          <p className="mt-0.5 text-xs text-[var(--muted)]">
            {t('reco.learningProgress', {
              current: Math.round(status.effectiveN * 10) / 10,
              target: status.targetN,
            })}
          </p>
        </div>
        <Link to="/taste" className="text-sm text-[var(--accent)] underline">
          {t('reco.teachTaste')}
        </Link>
      </div>
      <div className="mt-3 h-1.5 overflow-hidden rounded-full bg-black/40">
        <div
          className="h-full rounded-full bg-[var(--accent)] transition-[width]"
          style={{ width: `${Math.round(ratio * 100)}%` }}
        />
      </div>
    </div>
  )
}
