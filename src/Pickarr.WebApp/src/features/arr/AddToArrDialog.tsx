import { useMutation, useQuery } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fetchArrOptions, postTitleArr, type ArrAddBody } from '../../api/arr'
import { fetchSeriesSeasons } from '../../api/catalog'
import type { TitleDetail } from '../../api/types'
import { ApiError } from '../../api/client'

type Props = {
  title: TitleDetail
  open: boolean
  onClose: () => void
  onAdded: () => void
}

export function AddToArrDialog({ title, open, onClose, onAdded }: Props) {
  const { t } = useTranslation()
  const kind = (title.arr.kind === 'radarr' ? 'radarr' : 'sonarr') as 'sonarr' | 'radarr'
  const options = useQuery({
    queryKey: ['arr-options', kind],
    queryFn: () => fetchArrOptions(kind),
    enabled: open,
  })

  const [qualityProfileId, setQualityProfileId] = useState<number | ''>('')
  const [rootFolderPath, setRootFolderPath] = useState('')
  const [monitor, setMonitor] = useState('all')
  const [monitorNewItems, setMonitorNewItems] = useState('all')
  const [seriesType, setSeriesType] = useState('standard')
  const [seasonFolder, setSeasonFolder] = useState(true)
  const [monitored, setMonitored] = useState(true)
  const [minimumAvailability, setMinimumAvailability] = useState('released')
  const [searchNow, setSearchNow] = useState(true)
  const [tagIds, setTagIds] = useState<number[]>([])
  const [selectedSeasons, setSelectedSeasons] = useState<number[]>([])
  const [error, setError] = useState<string | null>(null)

  const seasons = useQuery({
    queryKey: ['title-seasons', title.id],
    queryFn: () => fetchSeriesSeasons(title.id),
    enabled: open && kind === 'sonarr',
  })

  useEffect(() => {
    if (!open || !seasons.data) return
    setSelectedSeasons(seasons.data.map((season) => season.number))
  }, [open, seasons.data])

  const choosingSeasons = kind === 'sonarr' && (seasons.data?.length ?? 0) > 0

  useEffect(() => {
    if (!options.data?.defaults) return
    setQualityProfileId(options.data.defaults.qualityProfileId ?? options.data.qualityProfiles[0]?.id ?? '')
    setRootFolderPath(options.data.defaults.rootFolderPath ?? options.data.rootFolders[0]?.path ?? '')
  }, [options.data])

  const canSubmit = useMemo(
    () =>
      open &&
      options.data?.configured === true &&
      options.data.reachable === true &&
      qualityProfileId !== '' &&
      rootFolderPath.length > 0 &&
      (!choosingSeasons || selectedSeasons.length > 0),
    [open, options.data, qualityProfileId, rootFolderPath, choosingSeasons, selectedSeasons],
  )

  const add = useMutation({
    mutationFn: async () => {
      if (qualityProfileId === '') throw new Error('profile required')
      const body: ArrAddBody =
        kind === 'sonarr'
          ? {
              qualityProfileId,
              rootFolderPath,
              monitor,
              monitorNewItems,
              seriesType,
              seasonFolder,
              tags: tagIds,
              searchNow,
              seasons: choosingSeasons ? selectedSeasons : undefined,
            }
          : {
              qualityProfileId,
              rootFolderPath,
              monitored,
              minimumAvailability,
              tags: tagIds,
              searchNow,
            }
      return postTitleArr(title.id, body)
    },
    onSuccess: () => {
      setError(null)
      onAdded()
      onClose()
    },
    onError: (err: unknown) => {
      if (err instanceof ApiError) {
        setError(err.message)
        return
      }
      setError(t('arr.addFailed'))
    },
  })

  if (!open) return null

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 p-4">
      <div
        role="dialog"
        aria-modal="true"
        className="w-[min(480px,92vw)] max-h-[90vh] overflow-y-auto rounded-xl border border-white/10 bg-[var(--surface)] p-5 text-[var(--text)] shadow-2xl"
      >
      <h3 className="text-lg font-semibold">{t('arr.canAdd', { kind: kind === 'sonarr' ? 'Sonarr' : 'Radarr' })}</h3>
      <p className="mt-1 text-sm text-[var(--muted)]">{title.name}</p>

      {options.isLoading && <p className="mt-4 text-sm text-[var(--muted)]">{t('catalog.loading')}</p>}
      {options.data && !options.data.configured && (
        <p className="mt-4 text-sm text-[var(--muted)]">{t('arr.notConfigured')}</p>
      )}
      {options.data && options.data.configured && !options.data.reachable && (
        <p className="mt-4 text-sm text-amber-300">{t('arr.unreachable')}</p>
      )}

      {options.data?.configured && options.data.reachable && (
        <form
          className="mt-4 space-y-3"
          onSubmit={(e) => {
            e.preventDefault()
            add.mutate()
          }}
        >
          <label className="block text-sm">
            <span className="text-[var(--muted)]">{t('settings.qualityProfile')}</span>
            <select
              className="mt-1 w-full rounded-md border border-white/10 bg-[var(--surface-2)] px-2 py-1.5"
              value={qualityProfileId}
              onChange={(e) => setQualityProfileId(Number(e.target.value))}
            >
              {options.data.qualityProfiles.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </label>

          <label className="block text-sm">
            <span className="text-[var(--muted)]">{t('settings.rootFolder')}</span>
            <select
              className="mt-1 w-full rounded-md border border-white/10 bg-[var(--surface-2)] px-2 py-1.5"
              value={rootFolderPath}
              onChange={(e) => setRootFolderPath(e.target.value)}
            >
              {options.data.rootFolders.map((r) => (
                <option key={r.path} value={r.path}>
                  {r.path}
                </option>
              ))}
            </select>
          </label>

          {kind === 'sonarr' ? (
            <>
              {seasons.isLoading && (
                <p className="text-sm text-[var(--muted)]">{t('title.seasonsLoading')}</p>
              )}
              {choosingSeasons && (
                <fieldset className="text-sm">
                  <legend className="text-[var(--muted)]">{t('arr.seasons')}</legend>
                  <p className="mt-1 text-xs text-[var(--muted)]">{t('arr.seasonsHint')}</p>
                  <div className="mt-2 flex gap-3 text-xs">
                    <button
                      type="button"
                      className="underline"
                      onClick={() => setSelectedSeasons(seasons.data?.map((season) => season.number) ?? [])}
                    >
                      {t('arr.selectAll')}
                    </button>
                    <button type="button" className="underline" onClick={() => setSelectedSeasons([])}>
                      {t('arr.selectNone')}
                    </button>
                  </div>
                  <div className="mt-2 max-h-40 space-y-1 overflow-y-auto rounded-md border border-white/10 p-2">
                    {seasons.data?.map((season) => {
                      const checked = selectedSeasons.includes(season.number)
                      const label =
                        season.number === 0
                          ? t('title.specials')
                          : `${t('title.season')} ${season.number}`
                      return (
                        <label key={season.number} className="flex items-center gap-2">
                          <input
                            type="checkbox"
                            checked={checked}
                            onChange={(e) =>
                              setSelectedSeasons((prev) =>
                                e.target.checked
                                  ? [...prev, season.number].sort((a, b) => b - a)
                                  : prev.filter((number) => number !== season.number),
                              )
                            }
                          />
                          <span>
                            {label}
                            {season.name ? ` · ${season.name}` : ''}
                            {` · ${t('title.episodeCount', { count: season.episodeCount })}`}
                          </span>
                        </label>
                      )
                    })}
                  </div>
                  {selectedSeasons.length === 0 && (
                    <p className="mt-1 text-xs text-amber-300">{t('arr.seasonRequired')}</p>
                  )}
                </fieldset>
              )}
              {!seasons.isLoading && !choosingSeasons && (
              <label className="block text-sm">
                <span className="text-[var(--muted)]">{t('arr.monitor')}</span>
                <select
                  className="mt-1 w-full rounded-md border border-white/10 bg-[var(--surface-2)] px-2 py-1.5"
                  value={monitor}
                  onChange={(e) => setMonitor(e.target.value)}
                >
                  {(options.data.monitorOptions ?? []).map((m) => (
                    <option key={m} value={m}>
                      {t(`arr.monitorModes.${m}`, { defaultValue: m })}
                    </option>
                  ))}
                </select>
              </label>
              )}
              <label className="block text-sm">
                <span className="text-[var(--muted)]">{t('arr.monitorNewItems')}</span>
                <select
                  className="mt-1 w-full rounded-md border border-white/10 bg-[var(--surface-2)] px-2 py-1.5"
                  value={monitorNewItems}
                  onChange={(e) => setMonitorNewItems(e.target.value)}
                >
                  <option value="all">{t('arr.monitorNew.all')}</option>
                  <option value="none">{t('arr.monitorNew.none')}</option>
                </select>
              </label>
              <label className="block text-sm">
                <span className="text-[var(--muted)]">{t('arr.seriesType')}</span>
                <select
                  className="mt-1 w-full rounded-md border border-white/10 bg-[var(--surface-2)] px-2 py-1.5"
                  value={seriesType}
                  onChange={(e) => setSeriesType(e.target.value)}
                >
                  {(options.data.seriesTypes ?? []).map((s) => (
                    <option key={s} value={s}>
                      {s}
                    </option>
                  ))}
                </select>
              </label>
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={seasonFolder}
                  onChange={(e) => setSeasonFolder(e.target.checked)}
                />
                {t('arr.seasonFolder')}
              </label>
            </>
          ) : (
            <>
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={monitored}
                  onChange={(e) => setMonitored(e.target.checked)}
                />
                {t('title.monitored')}
              </label>
              <label className="block text-sm">
                <span className="text-[var(--muted)]">{t('arr.minimumAvailability')}</span>
                <select
                  className="mt-1 w-full rounded-md border border-white/10 bg-[var(--surface-2)] px-2 py-1.5"
                  value={minimumAvailability}
                  onChange={(e) => setMinimumAvailability(e.target.value)}
                >
                  {(options.data.minimumAvailabilityOptions ?? []).map((m) => (
                    <option key={m} value={m}>
                      {t(`arr.availability.${m}`, { defaultValue: m })}
                    </option>
                  ))}
                </select>
              </label>
            </>
          )}

          {options.data.tags.length > 0 && (
            <fieldset className="text-sm">
              <legend className="text-[var(--muted)]">{t('arr.tags')}</legend>
              <div className="mt-1 flex flex-wrap gap-2">
                {options.data.tags.map((tag) => {
                  const checked = tagIds.includes(tag.id)
                  return (
                    <label key={tag.id} className="flex items-center gap-1.5 rounded bg-white/5 px-2 py-1">
                      <input
                        type="checkbox"
                        checked={checked}
                        onChange={(e) =>
                          setTagIds((prev) =>
                            e.target.checked ? [...prev, tag.id] : prev.filter((id) => id !== tag.id),
                          )
                        }
                      />
                      {tag.label}
                    </label>
                  )
                })}
              </div>
            </fieldset>
          )}

          <label className="flex items-center gap-2 text-sm">
            <input type="checkbox" checked={searchNow} onChange={(e) => setSearchNow(e.target.checked)} />
            {kind === 'sonarr' ? t('arr.searchMissing') : t('arr.searchMovie')}
          </label>

          {error && <p className="text-sm text-red-300">{error}</p>}

          <div className="flex justify-end gap-2 pt-2">
            <button
              type="button"
              onClick={onClose}
              className="rounded-md px-3 py-1.5 text-sm text-[var(--muted)] hover:bg-white/5"
            >
              {t('title.close')}
            </button>
            <button
              type="submit"
              disabled={!canSubmit || add.isPending}
              className="rounded-md bg-emerald-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-emerald-500 disabled:opacity-50"
            >
              {t('arr.confirmAdd')}
            </button>
          </div>
        </form>
      )}

      {(!options.data?.configured || !options.data?.reachable) && (
        <div className="mt-4 flex justify-end">
          <button
            type="button"
            onClick={onClose}
            className="rounded-md px-3 py-1.5 text-sm text-[var(--muted)] hover:bg-white/5"
          >
            {t('title.close')}
          </button>
        </div>
      )}
      </div>
    </div>
  )
}
