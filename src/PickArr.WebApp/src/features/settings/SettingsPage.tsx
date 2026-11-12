import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fetchArrOptions, postArrTest, type ArrTestResponse } from '../../api/arr'
import { fetchSettings, postConnectionTest, updateSettings } from '../../api/settings'

type ArrForm = {
  url: string
  apiKey: string
  qualityProfileId: string
  rootFolderPath: string
}

type ArrLists = {
  profiles: Array<{ id: number; name: string }>
  roots: Array<{ path: string }>
}

const emptyArr = (): ArrForm => ({
  url: '',
  apiKey: '',
  qualityProfileId: '',
  rootFolderPath: '',
})

function listsFromTest(result: ArrTestResponse): ArrLists {
  return {
    profiles: result.qualityProfiles ?? [],
    roots: (result.rootFolders ?? []).map((root) => ({ path: root.path })),
  }
}

export function SettingsPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const settings = useQuery({ queryKey: ['settings'], queryFn: fetchSettings })

  const [sonarr, setSonarr] = useState<ArrForm>(emptyArr())
  const [radarr, setRadarr] = useState<ArrForm>(emptyArr())
  const [watchmodeKey, setWatchmodeKey] = useState('')
  const [tmdbApiKey, setTmdbApiKey] = useState('')
  const [omdbApiKey, setOmdbApiKey] = useState('')
  const [plan, setPlan] = useState('free')
  const [quota, setQuota] = useState(2500)
  const [reservePercent, setReservePercent] = useState(10)
  const [maxCreditsPerRun, setMaxCreditsPerRun] = useState(80)
  const [recoEnabled, setRecoEnabled] = useState(true)
  const [saved, setSaved] = useState(false)
  const [sonarrTest, setSonarrTest] = useState<string | null>(null)
  const [radarrTest, setRadarrTest] = useState<string | null>(null)
  const [watchmodeTest, setWatchmodeTest] = useState<string | null>(null)
  const [tmdbTest, setTmdbTest] = useState<string | null>(null)
  const [omdbTest, setOmdbTest] = useState<string | null>(null)
  const [testing, setTesting] = useState<'sonarr' | 'radarr' | 'watchmode' | 'tmdb' | 'omdb' | null>(
    null,
  )
  const [sonarrLists, setSonarrLists] = useState<ArrLists | null>(null)
  const [radarrLists, setRadarrLists] = useState<ArrLists | null>(null)
  const [sonarrCredentialsDirty, setSonarrCredentialsDirty] = useState(false)
  const [radarrCredentialsDirty, setRadarrCredentialsDirty] = useState(false)

  const sonarrOptions = useQuery({
    queryKey: ['arr-options', 'sonarr'],
    queryFn: () => fetchArrOptions('sonarr'),
    enabled:
      sonarrLists == null &&
      !sonarrCredentialsDirty &&
      Boolean(settings.data?.sonarr.apiKey.isSet && settings.data.sonarr.url),
    retry: false,
  })
  const radarrOptions = useQuery({
    queryKey: ['arr-options', 'radarr'],
    queryFn: () => fetchArrOptions('radarr'),
    enabled:
      radarrLists == null &&
      !radarrCredentialsDirty &&
      Boolean(settings.data?.radarr.apiKey.isSet && settings.data.radarr.url),
    retry: false,
  })

  useEffect(() => {
    if (!settings.data) return
    setSonarr({
      url: settings.data.sonarr.url ?? '',
      apiKey: '',
      qualityProfileId: settings.data.sonarr.defaults?.qualityProfileId?.toString() ?? '',
      rootFolderPath: settings.data.sonarr.defaults?.rootFolderPath ?? '',
    })
    setRadarr({
      url: settings.data.radarr.url ?? '',
      apiKey: '',
      qualityProfileId: settings.data.radarr.defaults?.qualityProfileId?.toString() ?? '',
      rootFolderPath: settings.data.radarr.defaults?.rootFolderPath ?? '',
    })
    setRecoEnabled(settings.data.reco.enabled !== false)
    setPlan(settings.data.watchmode.plan || 'free')
    setQuota(settings.data.watchmode.quota)
    setReservePercent(settings.data.watchmode.reservePercent)
    setMaxCreditsPerRun(settings.data.watchmode.maxCreditsPerRun)
  }, [settings.data])

  useEffect(() => {
    if (sonarrCredentialsDirty || sonarrLists || !sonarrOptions.data?.defaults) return
    setSonarr((s) => ({
      ...s,
      qualityProfileId:
        s.qualityProfileId || String(sonarrOptions.data.defaults?.qualityProfileId ?? ''),
      rootFolderPath: s.rootFolderPath || sonarrOptions.data.defaults?.rootFolderPath || '',
    }))
  }, [sonarrOptions.data, sonarrCredentialsDirty, sonarrLists])

  useEffect(() => {
    if (radarrCredentialsDirty || radarrLists || !radarrOptions.data?.defaults) return
    setRadarr((s) => ({
      ...s,
      qualityProfileId:
        s.qualityProfileId || String(radarrOptions.data.defaults?.qualityProfileId ?? ''),
      rootFolderPath: s.rootFolderPath || radarrOptions.data.defaults?.rootFolderPath || '',
    }))
  }, [radarrOptions.data, radarrCredentialsDirty, radarrLists])

  const save = useMutation({
    mutationFn: () =>
      updateSettings({
        sonarr: {
          url: sonarr.url,
          apiKey: sonarr.apiKey.length > 0 ? sonarr.apiKey : undefined,
          defaults: {
            qualityProfileId: sonarr.qualityProfileId ? Number(sonarr.qualityProfileId) : null,
            rootFolderPath: sonarr.rootFolderPath || null,
          },
        },
        radarr: {
          url: radarr.url,
          apiKey: radarr.apiKey.length > 0 ? radarr.apiKey : undefined,
          defaults: {
            qualityProfileId: radarr.qualityProfileId ? Number(radarr.qualityProfileId) : null,
            rootFolderPath: radarr.rootFolderPath || null,
          },
        },
        watchmode: {
          apiKey: watchmodeKey.length > 0 ? watchmodeKey : undefined,
          plan,
          quota,
          reservePercent,
          maxCreditsPerRun,
        },
        tmdb: tmdbApiKey.length > 0 ? { apiKey: tmdbApiKey } : undefined,
        omdb: omdbApiKey.length > 0 ? { apiKey: omdbApiKey } : undefined,
        reco: {
          enabled: recoEnabled,
        },
      }),
    onSuccess: async () => {
      setWatchmodeKey('')
      setTmdbApiKey('')
      setOmdbApiKey('')
      setSonarr((s) => ({ ...s, apiKey: '' }))
      setRadarr((s) => ({ ...s, apiKey: '' }))
      setSaved(true)
      await queryClient.invalidateQueries({ queryKey: ['settings'] })
      await queryClient.invalidateQueries({ queryKey: ['titles'] })
      await queryClient.invalidateQueries({ queryKey: ['arr-options'] })
    },
  })

  const updateSonarr = (next: ArrForm) => {
    if (next.url !== sonarr.url || next.apiKey !== sonarr.apiKey) {
      setSonarrCredentialsDirty(true)
      setSonarrLists(null)
      setSonarrTest(null)
      next = { ...next, qualityProfileId: '', rootFolderPath: '' }
    }
    setSonarr(next)
  }

  const updateRadarr = (next: ArrForm) => {
    if (next.url !== radarr.url || next.apiKey !== radarr.apiKey) {
      setRadarrCredentialsDirty(true)
      setRadarrLists(null)
      setRadarrTest(null)
      next = { ...next, qualityProfileId: '', rootFolderPath: '' }
    }
    setRadarr(next)
  }

  const applyTestResult = (kind: 'sonarr' | 'radarr', result: ArrTestResponse) => {
    const message = result.ok
      ? t('settings.testOk', { version: result.version ?? '?' })
      : t('settings.testFail', { error: result.error ?? 'error' })
    const lists = listsFromTest(result)
    if (kind === 'sonarr') {
      setSonarrTest(message)
      if (!result.ok) return
      setSonarrLists(lists)
      setSonarrCredentialsDirty(false)
      setSonarr((current) => ({
        ...current,
        qualityProfileId: current.qualityProfileId || String(lists.profiles[0]?.id ?? ''),
        rootFolderPath: current.rootFolderPath || lists.roots[0]?.path || '',
      }))
      return
    }

    setRadarrTest(message)
    if (!result.ok) return
    setRadarrLists(lists)
    setRadarrCredentialsDirty(false)
    setRadarr((current) => ({
      ...current,
      qualityProfileId: current.qualityProfileId || String(lists.profiles[0]?.id ?? ''),
      rootFolderPath: current.rootFolderPath || lists.roots[0]?.path || '',
    }))
  }

  const runTest = async (kind: 'sonarr' | 'radarr') => {
    const form = kind === 'sonarr' ? sonarr : radarr
    setTesting(kind)
    try {
      const result = await postArrTest({
        kind,
        url: form.url || undefined,
        apiKey: form.apiKey || undefined,
      })
      applyTestResult(kind, result)
    } catch (err: unknown) {
      const message = t('settings.testFail', {
        error: err instanceof Error ? err.message : 'error',
      })
      if (kind === 'sonarr') setSonarrTest(message)
      else setRadarrTest(message)
    } finally {
      setTesting(null)
    }
  }

  const runConnectionTest = async (service: 'watchmode' | 'tmdb' | 'omdb') => {
    setTesting(service)
    const draft = service === 'watchmode' ? watchmodeKey : service === 'tmdb' ? tmdbApiKey : omdbApiKey
    try {
      const result = await postConnectionTest({
        service,
        apiKey: draft || undefined,
      })
      const message = result.ok
        ? t('settings.connectionTestOk', { detail: result.detail ?? '' })
        : t('settings.connectionTestFail', { error: result.detail ?? 'error' })
      if (service === 'watchmode') setWatchmodeTest(message)
      else if (service === 'tmdb') setTmdbTest(message)
      else setOmdbTest(message)
    } catch (err: unknown) {
      const message = t('settings.connectionTestFail', {
        error: err instanceof Error ? err.message : 'error',
      })
      if (service === 'watchmode') setWatchmodeTest(message)
      else if (service === 'tmdb') setTmdbTest(message)
      else setOmdbTest(message)
    } finally {
      setTesting(null)
    }
  }

  const env = settings.data?.environmentKeys
  const locked = (key: string) => Boolean(env?.includes(key))

  const sonarrProfiles =
    sonarrLists?.profiles ??
    (sonarrCredentialsDirty ? [] : (sonarrOptions.data?.qualityProfiles ?? []))
  const sonarrRoots =
    sonarrLists?.roots ?? (sonarrCredentialsDirty ? [] : (sonarrOptions.data?.rootFolders ?? []))
  const radarrProfiles =
    radarrLists?.profiles ??
    (radarrCredentialsDirty ? [] : (radarrOptions.data?.qualityProfiles ?? []))
  const radarrRoots =
    radarrLists?.roots ?? (radarrCredentialsDirty ? [] : (radarrOptions.data?.rootFolders ?? []))

  if (settings.isLoading) {
    return <p className="text-[var(--muted)]">{t('catalog.loading')}</p>
  }

  return (
    <div className="space-y-8">
      <div>
        <h2 className="text-2xl font-semibold tracking-tight">{t('settings.connections')}</h2>
        <p className="mt-2 text-sm text-[var(--muted)]">{t('settings.connectionsIntro')}</p>
      </div>

      <div className="grid gap-6 xl:grid-cols-2">
        <ArrSection
          title={t('settings.sonarr')}
          form={sonarr}
          onChange={updateSonarr}
          apiKeySet={settings.data?.sonarr.apiKey.isSet ?? false}
          profiles={sonarrProfiles}
          roots={sonarrRoots}
          testMessage={sonarrTest}
          testing={testing === 'sonarr'}
          onTest={() => void runTest('sonarr')}
        />

        <ArrSection
          title={t('settings.radarr')}
          form={radarr}
          onChange={updateRadarr}
          apiKeySet={settings.data?.radarr.apiKey.isSet ?? false}
          profiles={radarrProfiles}
          roots={radarrRoots}
          testMessage={radarrTest}
          testing={testing === 'radarr'}
          onTest={() => void runTest('radarr')}
        />
      </div>

      <div className="grid gap-6 xl:grid-cols-2">
        <section className="space-y-3 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
          <h2 className="text-lg font-medium">{t('settings.watchmode')}</h2>
          <label className="block space-y-1 text-sm">
            <span className="text-[var(--muted)]">{t('settings.apiKey')}</span>
            <input
              type="password"
              autoComplete="off"
              disabled={locked('watchmode.apiKey')}
              value={watchmodeKey}
              onChange={(e) => setWatchmodeKey(e.target.value)}
              placeholder={settings.data?.watchmode.apiKey.isSet ? t('settings.apiKeySet') : ''}
              className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2 disabled:opacity-60"
            />
            {locked('watchmode.apiKey') && (
              <p className="text-xs text-[var(--muted)]">{t('settings.envLocked')}</p>
            )}
          </label>
          <div className="grid gap-3 sm:grid-cols-2">
            <label className="block space-y-1 text-sm">
              <span className="text-[var(--muted)]">{t('settings.watchmodePlan')}</span>
              <select
                disabled={locked('watchmode.plan')}
                value={plan}
                onChange={(e) => setPlan(e.target.value)}
                className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2 disabled:opacity-60"
              >
                <option value="free">{t('settings.planFree')}</option>
                <option value="paid">{t('settings.planPaid')}</option>
              </select>
            </label>
            <label className="block space-y-1 text-sm">
              <span className="text-[var(--muted)]">{t('settings.watchmodeQuota')}</span>
              <input
                type="number"
                disabled={locked('watchmode.quota')}
                value={quota}
                onChange={(e) => setQuota(Number(e.target.value))}
                className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2 disabled:opacity-60"
              />
            </label>
            <label className="block space-y-1 text-sm">
              <span className="text-[var(--muted)]">{t('settings.watchmodeReserve')}</span>
              <input
                type="number"
                disabled={locked('watchmode.reservePercent')}
                value={reservePercent}
                onChange={(e) => setReservePercent(Number(e.target.value))}
                className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2 disabled:opacity-60"
              />
            </label>
            <label className="block space-y-1 text-sm">
              <span className="text-[var(--muted)]">{t('settings.watchmodeMaxCredits')}</span>
              <input
                type="number"
                disabled={locked('watchmode.maxCreditsPerRun')}
                value={maxCreditsPerRun}
                onChange={(e) => setMaxCreditsPerRun(Number(e.target.value))}
                className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2 disabled:opacity-60"
              />
            </label>
          </div>
          <div className="flex flex-wrap items-center gap-3">
            <button
              type="button"
              onClick={() => void runConnectionTest('watchmode')}
              disabled={testing === 'watchmode'}
              className="rounded-md bg-white/10 px-3 py-1.5 text-sm hover:bg-white/15 disabled:opacity-60"
            >
              {testing === 'watchmode' ? t('settings.testing') : t('settings.testConnection')}
            </button>
            {watchmodeTest && <span className="text-sm text-[var(--muted)]">{watchmodeTest}</span>}
          </div>
        </section>

        <section className="space-y-3 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
          <h2 className="text-lg font-medium">{t('settings.tmdb')}</h2>
          <label className="block space-y-1 text-sm">
            <span className="text-[var(--muted)]">{t('settings.apiKey')}</span>
            <input
              type="password"
              autoComplete="off"
              disabled={locked('tmdb.apiKey')}
              value={tmdbApiKey}
              onChange={(e) => setTmdbApiKey(e.target.value)}
              placeholder={settings.data?.tmdb.apiKey.isSet ? t('settings.apiKeySet') : ''}
              className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2 disabled:opacity-60"
            />
            <p className="text-xs text-[var(--muted)]">{t('settings.tmdbApiKeyHint')}</p>
            {locked('tmdb.apiKey') && (
              <p className="text-xs text-[var(--muted)]">{t('settings.envLocked')}</p>
            )}
          </label>
          <div className="flex flex-wrap items-center gap-3">
            <button
              type="button"
              onClick={() => void runConnectionTest('tmdb')}
              disabled={testing === 'tmdb'}
              className="rounded-md bg-white/10 px-3 py-1.5 text-sm hover:bg-white/15 disabled:opacity-60"
            >
              {testing === 'tmdb' ? t('settings.testing') : t('settings.testConnection')}
            </button>
            {tmdbTest && <span className="text-sm text-[var(--muted)]">{tmdbTest}</span>}
          </div>
        </section>

        <section className="space-y-3 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
          <h2 className="text-lg font-medium">{t('settings.omdb')}</h2>
          <label className="block space-y-1 text-sm">
            <span className="text-[var(--muted)]">{t('settings.apiKey')}</span>
            <input
              type="password"
              autoComplete="off"
              disabled={locked('omdb.apiKey')}
              value={omdbApiKey}
              onChange={(e) => setOmdbApiKey(e.target.value)}
              placeholder={settings.data?.omdb.apiKey.isSet ? t('settings.apiKeySet') : ''}
              className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2 disabled:opacity-60"
            />
            <p className="text-xs text-[var(--muted)]">{t('settings.omdbApiKeyHint')}</p>
            {locked('omdb.apiKey') && (
              <p className="text-xs text-[var(--muted)]">{t('settings.envLocked')}</p>
            )}
          </label>
          <div className="flex flex-wrap items-center gap-3">
            <button
              type="button"
              onClick={() => void runConnectionTest('omdb')}
              disabled={testing === 'omdb'}
              className="rounded-md bg-white/10 px-3 py-1.5 text-sm hover:bg-white/15 disabled:opacity-60"
            >
              {testing === 'omdb' ? t('settings.testing') : t('settings.testConnection')}
            </button>
            {omdbTest && <span className="text-sm text-[var(--muted)]">{omdbTest}</span>}
          </div>
        </section>
      </div>

      <section className="space-y-3 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
        <h2 className="text-lg font-medium">{t('settings.recommendations')}</h2>
        <label className="flex items-start gap-3 text-sm">
          <input
            type="checkbox"
            className="mt-1"
            checked={recoEnabled}
            onChange={(e) => setRecoEnabled(e.target.checked)}
          />
          <span>
            <span className="block">{t('settings.recoEnabled')}</span>
            <span className="block text-xs text-[var(--muted)]">{t('settings.recoEnabledHint')}</span>
          </span>
        </label>
      </section>

      <div className="flex items-center gap-4">
        <button
          type="button"
          onClick={() => {
            setSaved(false)
            save.mutate()
          }}
          disabled={save.isPending}
          className="rounded-md bg-[var(--accent)] px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
        >
          {t('settings.save')}
        </button>
        {saved && !save.isPending && (
          <span className="text-sm text-emerald-300">{t('settings.saved')}</span>
        )}
      </div>
    </div>
  )
}

function ArrSection({
  title,
  form,
  onChange,
  apiKeySet,
  profiles,
  roots,
  testMessage,
  testing,
  onTest,
}: {
  title: string
  form: ArrForm
  onChange: (value: ArrForm) => void
  apiKeySet: boolean
  profiles: Array<{ id: number; name: string }>
  roots: Array<{ path: string }>
  testMessage: string | null
  testing: boolean
  onTest: () => void
}) {
  const { t } = useTranslation()
  return (
    <section className="space-y-3 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
      <h2 className="text-lg font-medium">{title}</h2>
      <label className="block space-y-1 text-sm">
        <span className="text-[var(--muted)]">{t('settings.url')}</span>
        <input
          type="url"
          value={form.url}
          onChange={(e) => onChange({ ...form, url: e.target.value })}
          className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2"
          placeholder="http://localhost:8989"
        />
      </label>
      <label className="block space-y-1 text-sm">
        <span className="text-[var(--muted)]">{t('settings.apiKey')}</span>
        <input
          type="password"
          autoComplete="off"
          value={form.apiKey}
          onChange={(e) => onChange({ ...form, apiKey: e.target.value })}
          placeholder={apiKeySet ? t('settings.apiKeySet') : ''}
          className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2"
        />
      </label>
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="block space-y-1 text-sm">
          <span className="text-[var(--muted)]">{t('settings.qualityProfile')}</span>
          <select
            className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2"
            value={form.qualityProfileId}
            onChange={(e) => onChange({ ...form, qualityProfileId: e.target.value })}
          >
            <option value="">{t('settings.selectDefault')}</option>
            {profiles.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
        </label>
        <label className="block space-y-1 text-sm">
          <span className="text-[var(--muted)]">{t('settings.rootFolder')}</span>
          <select
            className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2"
            value={form.rootFolderPath}
            onChange={(e) => onChange({ ...form, rootFolderPath: e.target.value })}
          >
            <option value="">{t('settings.selectDefault')}</option>
            {roots.map((r) => (
              <option key={r.path} value={r.path}>
                {r.path}
              </option>
            ))}
          </select>
        </label>
      </div>
      <p className="text-sm text-[var(--muted)]">
        {profiles.length > 0 || roots.length > 0 ? t('settings.optionsReady') : t('settings.optionsHint')}
      </p>
      <div className="flex flex-wrap items-center gap-3">
        <button
          type="button"
          onClick={onTest}
          disabled={testing}
          className="rounded-md bg-white/10 px-3 py-1.5 text-sm hover:bg-white/15 disabled:opacity-60"
        >
          {testing ? t('settings.testing') : t('settings.testConnection')}
        </button>
        {testMessage && <span className="text-sm text-[var(--muted)]">{testMessage}</span>}
      </div>
    </section>
  )
}
