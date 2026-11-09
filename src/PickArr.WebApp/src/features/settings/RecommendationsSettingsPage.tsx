import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  fetchRecoStatus,
  postRecoEmbeddingsRebuild,
  postRecoEvaluate,
  postRecoModelDetect,
  postRecoModelDownload,
  postRecoRebuild,
  postRecoReset,
} from '../../api/recommendations'
import { fetchSettings, updateSettings } from '../../api/settings'

const DEFAULT_EXPLORE_RATE = 0.1

export function RecommendationsSettingsPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const settings = useQuery({ queryKey: ['settings'], queryFn: fetchSettings })
  const status = useQuery({ queryKey: ['reco-status'], queryFn: fetchRecoStatus, refetchInterval: 5000 })

  const [enabled, setEnabled] = useState(true)
  const [useImplicit, setUseImplicit] = useState(true)
  const [embeddingsEnabled, setEmbeddingsEnabled] = useState(true)
  const [exploreRate, setExploreRate] = useState(DEFAULT_EXPLORE_RATE)
  const [excludeInLibrary, setExcludeInLibrary] = useState(true)
  const [n0, setN0] = useState(30)
  const [weightLove, setWeightLove] = useState(1.5)
  const [weightLike, setWeightLike] = useState(1)
  const [weightDislike, setWeightDislike] = useState(-1)
  const [weightFavorite, setWeightFavorite] = useState(0.5)
  const [weightArrAdd, setWeightArrAdd] = useState(0.6)
  const [weightDownloaded, setWeightDownloaded] = useState(0.4)
  const [weightWatched, setWeightWatched] = useState(0.35)
  const [mmrLambda, setMmrLambda] = useState(0.7)
  const [eventsEnabled, setEventsEnabled] = useState(false)
  const [saved, setSaved] = useState(false)
  const [message, setMessage] = useState<string | null>(null)

  useEffect(() => {
    if (!settings.data) return
    const reco = settings.data.reco
    setEnabled(reco.enabled !== false)
    setUseImplicit(reco.useImplicit !== false)
    setEmbeddingsEnabled(reco.embeddingsEnabled !== false)
    setExploreRate(typeof reco.exploreRate === 'number' ? reco.exploreRate : DEFAULT_EXPLORE_RATE)
    setExcludeInLibrary(reco.excludeInLibrary !== false)
    setN0(reco.n0 ?? 30)
    setWeightLove(reco.weightLove ?? 1.5)
    setWeightLike(reco.weightLike ?? 1)
    setWeightDislike(reco.weightDislike ?? -1)
    setWeightFavorite(reco.weightFavorite ?? 0.5)
    setWeightArrAdd(reco.weightArrAdd ?? 0.6)
    setWeightDownloaded(reco.weightDownloaded ?? 0.4)
    setWeightWatched(reco.weightWatched ?? 0.35)
    setMmrLambda(reco.mmrLambda ?? 0.7)
    setEventsEnabled(reco.eventsEnabled === true)
  }, [settings.data])

  const exploreEnabled = exploreRate > 0

  const save = useMutation({
    mutationFn: () =>
      updateSettings({
        reco: {
          enabled,
          useImplicit,
          embeddingsEnabled,
          exploreRate,
          excludeInLibrary,
          n0,
          weightLove,
          weightLike,
          weightDislike,
          weightFavorite,
          weightArrAdd,
          weightDownloaded,
          weightWatched,
          mmrLambda,
          eventsEnabled,
        },
      }),
    onSuccess: async () => {
      setSaved(true)
      await queryClient.invalidateQueries({ queryKey: ['settings'] })
    },
  })

  const run = async (label: string, action: () => Promise<unknown>) => {
    setMessage(null)
    try {
      await action()
      setMessage(t('settings.recoActionQueued', { action: label }))
      await queryClient.invalidateQueries({ queryKey: ['reco-status'] })
    } catch {
      setMessage(t('settings.recoActionFailed', { action: label }))
    }
  }

  return (
    <div className="mx-auto max-w-2xl space-y-8">
      <div>
        <h2 className="text-2xl font-semibold tracking-tight">{t('settings.recommendations')}</h2>
        <p className="mt-2 text-sm text-[var(--muted)]">{t('settings.recoLocalNote')}</p>
      </div>

      <section className="space-y-3 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
        <label className="flex items-start gap-3 text-sm">
          <input type="checkbox" className="mt-1" checked={enabled} onChange={(e) => setEnabled(e.target.checked)} />
          <span>
            <span className="block">{t('settings.recoEnabled')}</span>
            <span className="block text-xs text-[var(--muted)]">{t('settings.recoEnabledHint')}</span>
          </span>
        </label>
        <label className="flex items-start gap-3 text-sm">
          <input
            type="checkbox"
            className="mt-1"
            checked={useImplicit}
            disabled={!enabled}
            onChange={(e) => setUseImplicit(e.target.checked)}
          />
          <span>
            <span className="block">{t('settings.recoUseImplicit')}</span>
            <span className="block text-xs text-[var(--muted)]">{t('settings.recoUseImplicitHint')}</span>
          </span>
        </label>
        <label className="flex items-start gap-3 text-sm">
          <input
            type="checkbox"
            className="mt-1"
            checked={embeddingsEnabled}
            disabled={!enabled}
            onChange={(e) => setEmbeddingsEnabled(e.target.checked)}
          />
          <span>
            <span className="block">{t('settings.recoEmbeddingsEnabled')}</span>
            <span className="block text-xs text-[var(--muted)]">{t('settings.recoEmbeddingsEnabledHint')}</span>
          </span>
        </label>
        <label className="flex items-start gap-3 text-sm">
          <input
            type="checkbox"
            className="mt-1"
            checked={exploreEnabled}
            disabled={!enabled}
            onChange={(e) => setExploreRate(e.target.checked ? DEFAULT_EXPLORE_RATE : 0)}
          />
          <span>
            <span className="block">{t('settings.recoExploreEnabled')}</span>
            <span className="block text-xs text-[var(--muted)]">{t('settings.recoExploreEnabledHint')}</span>
          </span>
        </label>
        <label className="flex items-start gap-3 text-sm">
          <input
            type="checkbox"
            className="mt-1"
            checked={eventsEnabled}
            disabled={!enabled}
            onChange={(e) => setEventsEnabled(e.target.checked)}
          />
          <span>
            <span className="block">{t('settings.recoEventsEnabled')}</span>
            <span className="block text-xs text-[var(--muted)]">{t('settings.recoEventsEnabledHint')}</span>
          </span>
        </label>

        <details className="rounded-lg border border-white/10 bg-black/20 p-3">
          <summary className="cursor-pointer text-sm font-medium">{t('settings.recoAdvanced')}</summary>
          <div className="mt-3 grid gap-3 sm:grid-cols-2">
            <label className="flex items-start gap-3 text-sm sm:col-span-2">
              <input
                type="checkbox"
                className="mt-1"
                checked={excludeInLibrary}
                disabled={!enabled}
                onChange={(e) => setExcludeInLibrary(e.target.checked)}
              />
              <span>
                <span className="block">{t('settings.recoExcludeInLibrary')}</span>
                <span className="block text-xs text-[var(--muted)]">{t('settings.recoExcludeInLibraryHint')}</span>
              </span>
            </label>
            <NumberField label={t('settings.recoN0')} value={n0} disabled={!enabled} onChange={setN0} step={1} />
            <NumberField
              label={t('settings.recoExploreRate')}
              value={exploreRate}
              disabled={!enabled}
              onChange={setExploreRate}
              step={0.05}
            />
            <NumberField
              label={t('settings.recoMmrLambda')}
              value={mmrLambda}
              disabled={!enabled}
              onChange={setMmrLambda}
              step={0.05}
            />
            <NumberField
              label={t('settings.recoWeightLove')}
              value={weightLove}
              disabled={!enabled}
              onChange={setWeightLove}
              step={0.1}
            />
            <NumberField
              label={t('settings.recoWeightLike')}
              value={weightLike}
              disabled={!enabled}
              onChange={setWeightLike}
              step={0.1}
            />
            <NumberField
              label={t('settings.recoWeightDislike')}
              value={weightDislike}
              disabled={!enabled}
              onChange={setWeightDislike}
              step={0.1}
            />
            <NumberField
              label={t('settings.recoWeightFavorite')}
              value={weightFavorite}
              disabled={!enabled}
              onChange={setWeightFavorite}
              step={0.1}
            />
            <NumberField
              label={t('settings.recoWeightArrAdd')}
              value={weightArrAdd}
              disabled={!enabled}
              onChange={setWeightArrAdd}
              step={0.1}
            />
            <NumberField
              label={t('settings.recoWeightDownloaded')}
              value={weightDownloaded}
              disabled={!enabled}
              onChange={setWeightDownloaded}
              step={0.1}
            />
            <NumberField
              label={t('settings.recoWeightWatched')}
              value={weightWatched}
              disabled={!enabled}
              onChange={setWeightWatched}
              step={0.1}
            />
          </div>
        </details>

        <button
          type="button"
          onClick={() => {
            setSaved(false)
            save.mutate()
          }}
          className="rounded-md bg-[var(--accent)] px-4 py-2 text-sm font-medium text-white"
        >
          {t('settings.save')}
        </button>
        {saved && <span className="ml-3 text-sm text-emerald-300">{t('settings.saved')}</span>}
      </section>

      <section className="space-y-2 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5 text-sm">
        <h3 className="text-lg font-medium">{t('settings.recoStatus')}</h3>
        {status.isLoading && <p className="text-[var(--muted)]">{t('catalog.loading')}</p>}
        {status.data && (
          <ul className="space-y-1 text-[var(--muted)]">
            <li>
              {t('settings.recoStage')}: {status.data.stage}
            </li>
            <li>
              {t('settings.recoVotes')}: {status.data.ratingCount} · N_eff {status.data.effectiveN.toFixed(1)}
            </li>
            <li>
              {t('settings.recoPool')}: {status.data.pool.titles}
            </li>
            <li>
              {t('settings.recoModel')}: {status.data.model.status}
              {status.data.model.id ? ` (${status.data.model.id})` : ''}
            </li>
            <li>
              {t('settings.recoCoverage')}: {(status.data.embeddings.coverage * 100).toFixed(0)}%
            </li>
          </ul>
        )}
      </section>

      <section className="space-y-3 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
        <h3 className="text-lg font-medium">{t('settings.recoActions')}</h3>
        <div className="flex flex-wrap gap-2">
          <ActionButton label={t('settings.recoRebuild')} onClick={() => void run('rebuild', postRecoRebuild)} />
          <ActionButton label={t('settings.recoEvaluate')} onClick={() => void run('evaluate', postRecoEvaluate)} />
          <ActionButton
            label={t('settings.recoEmbeddings')}
            onClick={() => void run('embeddings', () => postRecoEmbeddingsRebuild(false))}
          />
          <ActionButton
            label={t('settings.recoModelDownload')}
            onClick={() => void run('download', postRecoModelDownload)}
          />
          <ActionButton label={t('settings.recoModelDetect')} onClick={() => void run('detect', postRecoModelDetect)} />
          <ActionButton
            label={t('settings.recoResetDerived')}
            onClick={() => {
              if (window.confirm(t('settings.recoResetDerivedConfirm'))) {
                void run('reset', () => postRecoReset(false))
              }
            }}
          />
          <ActionButton
            label={t('settings.recoResetAll')}
            onClick={() => {
              if (window.confirm(t('settings.recoResetAllConfirm'))) {
                void run('reset-all', () => postRecoReset(true))
              }
            }}
          />
        </div>
        {message && <p className="text-sm text-[var(--muted)]">{message}</p>}
      </section>
    </div>
  )
}

function NumberField({
  label,
  value,
  onChange,
  disabled,
  step,
}: {
  label: string
  value: number
  onChange: (value: number) => void
  disabled?: boolean
  step: number
}) {
  return (
    <label className="block text-sm">
      <span className="mb-1 block text-[var(--muted)]">{label}</span>
      <input
        type="number"
        step={step}
        disabled={disabled}
        value={Number.isFinite(value) ? value : 0}
        onChange={(e) => onChange(Number(e.target.value))}
        className="w-full rounded-md border border-white/10 bg-black/30 px-2 py-1.5"
      />
    </label>
  )
}

function ActionButton({ label, onClick }: { label: string; onClick: () => void }) {
  return (
    <button type="button" onClick={onClick} className="rounded-md bg-white/10 px-3 py-1.5 text-sm hover:bg-white/15">
      {label}
    </button>
  )
}
