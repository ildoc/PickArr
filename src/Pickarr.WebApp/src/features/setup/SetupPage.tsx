import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { useNavigate } from 'react-router-dom'
import { postArrTest } from '../../api/arr'
import { ApiError } from '../../api/client'
import { updateProfile } from '../../api/profiles'
import { completeSetup } from '../../api/setup'
import { fetchSettings, postConnectionTest, updateSettings } from '../../api/settings'
import {
  CatalogSourcesForm,
  type CatalogSourcesFormHandle,
} from '../settings/CatalogSourcesForm'
import { profilePalette, profileSwatch, type ProfileColor } from '../who/colors'
import { useProfiles } from '../who/profileContext'

const STEPS = 4

type ArrForm = {
  url: string
  apiKey: string
  qualityProfileId: string
  rootFolderPath: string
}

const emptyArr = (): ArrForm => ({
  url: '',
  apiKey: '',
  qualityProfileId: '',
  rootFolderPath: '',
})

function envLocked(keys: string[] | undefined, key: string) {
  return Boolean(keys?.includes(key))
}

export function SetupPage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const { profiles, choose } = useProfiles()
  const admin = profiles.find((profile) => profile.isAdmin) ?? profiles[0] ?? null
  const catalogsRef = useRef<CatalogSourcesFormHandle>(null)

  const settings = useQuery({ queryKey: ['settings'], queryFn: fetchSettings })
  const [step, setStep] = useState(1)
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  const [name, setName] = useState('Admin')
  const [color, setColor] = useState<ProfileColor>('blue')

  const [watchmodeKey, setWatchmodeKey] = useState('')
  const [tmdbKey, setTmdbKey] = useState('')
  const [plan, setPlan] = useState('free')
  const [quota, setQuota] = useState(2500)
  const [reservePercent, setReservePercent] = useState(10)
  const [maxCreditsPerRun, setMaxCreditsPerRun] = useState(80)
  const [recoEnabled, setRecoEnabled] = useState(true)
  const [watchmodeTest, setWatchmodeTest] = useState<string | null>(null)
  const [tmdbTest, setTmdbTest] = useState<string | null>(null)

  const [sonarr, setSonarr] = useState<ArrForm>(emptyArr())
  const [radarr, setRadarr] = useState<ArrForm>(emptyArr())
  const [sonarrTest, setSonarrTest] = useState<string | null>(null)
  const [radarrTest, setRadarrTest] = useState<string | null>(null)
  const [sonarrOk, setSonarrOk] = useState(false)
  const [radarrOk, setRadarrOk] = useState(false)
  const [sonarrLists, setSonarrLists] = useState<{
    profiles: Array<{ id: number; name: string }>
    roots: Array<{ path: string }>
  } | null>(null)
  const [radarrLists, setRadarrLists] = useState<{
    profiles: Array<{ id: number; name: string }>
    roots: Array<{ path: string }>
  } | null>(null)

  useEffect(() => {
    if (!admin) return
    setName(admin.name)
    setColor((admin.color as ProfileColor) || 'blue')
  }, [admin?.id, admin?.name, admin?.color])

  useEffect(() => {
    if (admin) choose(admin.id)
    // Select the admin once so personal API calls send X-Pickarr-Profile.
    // eslint-disable-next-line react-hooks/exhaustive-deps -- run when admin id appears
  }, [admin?.id])

  useEffect(() => {
    if (!settings.data) return
    setPlan(settings.data.watchmode.plan || 'free')
    setQuota(settings.data.watchmode.quota)
    setReservePercent(settings.data.watchmode.reservePercent)
    setMaxCreditsPerRun(settings.data.watchmode.maxCreditsPerRun)
    setRecoEnabled(settings.data.reco.enabled !== false)
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
  }, [settings.data])

  const env = settings.data?.environmentKeys
  const watchmodeLocked = envLocked(env, 'watchmode.apiKey')
  const tmdbLocked = envLocked(env, 'tmdb.apiKey')
  const planLocked = envLocked(env, 'watchmode.plan')
  const quotaLocked = envLocked(env, 'watchmode.quota')
  const reserveLocked = envLocked(env, 'watchmode.reservePercent')
  const maxCreditsLocked = envLocked(env, 'watchmode.maxCreditsPerRun')
  const recoLocked = envLocked(env, 'reco.enabled')
  const sonarrUrlLocked = envLocked(env, 'sonarr.url')
  const sonarrKeyLocked = envLocked(env, 'sonarr.apiKey')
  const radarrUrlLocked = envLocked(env, 'radarr.url')
  const radarrKeyLocked = envLocked(env, 'radarr.apiKey')

  const finish = useMutation({
    mutationFn: completeSetup,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['setup'] })
      await queryClient.invalidateQueries({ queryKey: ['settings'] })
      await queryClient.invalidateQueries({ queryKey: ['sources'] })
      navigate('/catalog', { replace: true })
    },
  })

  const runKeyTest = async (service: 'watchmode' | 'tmdb') => {
    setError(null)
    const draft = service === 'watchmode' ? watchmodeKey : tmdbKey
    try {
      const result = await postConnectionTest({
        service,
        apiKey: draft || undefined,
      })
      const message = result.ok
        ? t('setup.testOk', { detail: result.detail ?? '' })
        : t('setup.testFail', { error: result.detail ?? 'error' })
      if (service === 'watchmode') setWatchmodeTest(message)
      else setTmdbTest(message)
      return result.ok
    } catch (err) {
      const message = t('setup.testFail', {
        error: err instanceof Error ? err.message : 'error',
      })
      if (service === 'watchmode') setWatchmodeTest(message)
      else setTmdbTest(message)
      return false
    }
  }

  const runArrTest = async (kind: 'sonarr' | 'radarr') => {
    const form = kind === 'sonarr' ? sonarr : radarr
    try {
      const result = await postArrTest({
        kind,
        url: form.url || undefined,
        apiKey: form.apiKey || undefined,
      })
      const message = result.ok
        ? t('settings.testOk', { version: result.version ?? '?' })
        : t('settings.testFail', { error: result.error ?? 'error' })
      const lists = {
        profiles: result.qualityProfiles ?? [],
        roots: (result.rootFolders ?? []).map((root) => ({ path: root.path })),
      }
      if (kind === 'sonarr') {
        setSonarrTest(message)
        setSonarrOk(result.ok)
        if (result.ok) {
          setSonarrLists(lists)
          setSonarr((current) => ({
            ...current,
            qualityProfileId: current.qualityProfileId || String(lists.profiles[0]?.id ?? ''),
            rootFolderPath: current.rootFolderPath || lists.roots[0]?.path || '',
          }))
        }
      } else {
        setRadarrTest(message)
        setRadarrOk(result.ok)
        if (result.ok) {
          setRadarrLists(lists)
          setRadarr((current) => ({
            ...current,
            qualityProfileId: current.qualityProfileId || String(lists.profiles[0]?.id ?? ''),
            rootFolderPath: current.rootFolderPath || lists.roots[0]?.path || '',
          }))
        }
      }
      return result.ok
    } catch (err) {
      const message = t('settings.testFail', {
        error: err instanceof Error ? err.message : 'error',
      })
      if (kind === 'sonarr') {
        setSonarrTest(message)
        setSonarrOk(false)
      } else {
        setRadarrTest(message)
        setRadarrOk(false)
      }
      return false
    }
  }

  const saveStep1 = async () => {
    if (!admin) throw new Error(t('setup.adminMissing'))
    const trimmed = name.trim()
    if (trimmed.length < 1 || trimmed.length > 40) {
      throw new Error(t('setup.nameInvalid'))
    }
    await updateProfile(admin.id, { name: trimmed, color })
    await queryClient.invalidateQueries({ queryKey: ['profiles'] })
  }

  const saveStep2 = async () => {
    const needWatchmode = !settings.data?.watchmode.apiKey.isSet && !watchmodeKey && !watchmodeLocked
    const needTmdb = !settings.data?.tmdb.apiKey.isSet && !tmdbKey && !tmdbLocked
    if (needWatchmode || needTmdb) {
      throw new Error(t('setup.keysRequired'))
    }

    const wmOk = await runKeyTest('watchmode')
    const tmOk = await runKeyTest('tmdb')
    if (!wmOk || !tmOk) {
      throw new Error(t('setup.keysMustPass'))
    }

    await updateSettings({
      watchmode: {
        apiKey: watchmodeKey || undefined,
        plan: planLocked ? undefined : plan,
        quota: quotaLocked ? undefined : quota,
        reservePercent: reserveLocked ? undefined : reservePercent,
        maxCreditsPerRun: maxCreditsLocked ? undefined : maxCreditsPerRun,
      },
      tmdb: tmdbKey ? { apiKey: tmdbKey } : undefined,
      reco: recoLocked ? undefined : { enabled: recoEnabled },
    })
    setWatchmodeKey('')
    setTmdbKey('')
    await queryClient.invalidateQueries({ queryKey: ['settings'] })
  }

  const saveStep3 = async () => {
    const ok = await catalogsRef.current?.save()
    if (!ok) {
      throw new Error(t('setup.catalogsNeedSelection'))
    }
  }

  const arrConfigured = (form: ArrForm, keySet: boolean) =>
    Boolean(form.url.trim()) || Boolean(form.apiKey.trim()) || keySet

  const saveStep4 = async () => {
    const sonarrNeeded = arrConfigured(sonarr, settings.data?.sonarr.apiKey.isSet ?? false)
    const radarrNeeded = arrConfigured(radarr, settings.data?.radarr.apiKey.isSet ?? false)

    if (sonarrNeeded) {
      if (!sonarr.url.trim() && !settings.data?.sonarr.url) {
        throw new Error(t('setup.arrIncomplete', { name: t('settings.sonarr') }))
      }
      if (!sonarr.apiKey && !settings.data?.sonarr.apiKey.isSet) {
        throw new Error(t('setup.arrIncomplete', { name: t('settings.sonarr') }))
      }
      const ok = sonarrOk || (await runArrTest('sonarr'))
      if (!ok) throw new Error(t('setup.arrMustPass', { name: t('settings.sonarr') }))
    }

    if (radarrNeeded) {
      if (!radarr.url.trim() && !settings.data?.radarr.url) {
        throw new Error(t('setup.arrIncomplete', { name: t('settings.radarr') }))
      }
      if (!radarr.apiKey && !settings.data?.radarr.apiKey.isSet) {
        throw new Error(t('setup.arrIncomplete', { name: t('settings.radarr') }))
      }
      const ok = radarrOk || (await runArrTest('radarr'))
      if (!ok) throw new Error(t('setup.arrMustPass', { name: t('settings.radarr') }))
    }

    if (sonarrNeeded || radarrNeeded) {
      await updateSettings({
        sonarr: sonarrNeeded
          ? {
              url: sonarrUrlLocked ? undefined : sonarr.url,
              apiKey: sonarr.apiKey || undefined,
              defaults: {
                qualityProfileId: sonarr.qualityProfileId ? Number(sonarr.qualityProfileId) : null,
                rootFolderPath: sonarr.rootFolderPath || null,
              },
            }
          : undefined,
        radarr: radarrNeeded
          ? {
              url: radarrUrlLocked ? undefined : radarr.url,
              apiKey: radarr.apiKey || undefined,
              defaults: {
                qualityProfileId: radarr.qualityProfileId ? Number(radarr.qualityProfileId) : null,
                rootFolderPath: radarr.rootFolderPath || null,
              },
            }
          : undefined,
      })
      await queryClient.invalidateQueries({ queryKey: ['settings'] })
    }

    await finish.mutateAsync()
  }

  const goNext = async () => {
    setError(null)
    setBusy(true)
    try {
      if (step === 1) await saveStep1()
      else if (step === 2) await saveStep2()
      else if (step === 3) await saveStep3()
      else await saveStep4()

      if (step < STEPS) setStep((current) => current + 1)
    } catch (err) {
      setError(err instanceof ApiError ? err.message : err instanceof Error ? err.message : t('setup.saveFailed'))
    } finally {
      setBusy(false)
    }
  }

  if (settings.isLoading && !settings.data) {
    return <p className="text-[var(--muted)]">{t('admin.loading')}</p>
  }

  return (
    <div className="mx-auto w-full max-w-5xl space-y-8">
      <div>
        <p className="text-sm text-[var(--muted)]">{t('setup.stepOf', { step, total: STEPS })}</p>
        <h1 className="mt-1 text-3xl font-semibold tracking-tight">{t(`setup.step${step}Title`)}</h1>
        <p className="mt-2 text-sm text-[var(--muted)]">{t(`setup.step${step}Lead`)}</p>
      </div>

      {step === 1 && (
        <section className="space-y-4 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
          <label className="block space-y-1 text-sm">
            <span className="text-[var(--muted)]">{t('who.name')}</span>
            <input
              value={name}
              onChange={(e) => setName(e.target.value)}
              maxLength={40}
              className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2"
            />
          </label>
          <fieldset>
            <legend className="text-sm text-[var(--muted)]">{t('who.color')}</legend>
            <div className="mt-2 flex flex-wrap gap-2">
              {profilePalette.map((swatch) => (
                <button
                  key={swatch}
                  type="button"
                  aria-label={t('who.colorSwatch', { color: swatch })}
                  aria-pressed={color === swatch}
                  onClick={() => setColor(swatch)}
                  className={`h-9 w-9 rounded-full ring-2 ${
                    color === swatch ? 'ring-white' : 'ring-transparent'
                  }`}
                  style={{ background: profileSwatch(swatch) }}
                />
              ))}
            </div>
          </fieldset>
        </section>
      )}

      {step === 2 && (
        <div className="space-y-6">
          <section className="space-y-3 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
            <h2 className="text-lg font-medium">{t('settings.watchmode')}</h2>
            <p className="text-sm text-[var(--muted)]">{t('setup.watchmodeWhy')}</p>
            <a
              href="https://api.watchmode.com/"
              target="_blank"
              rel="noreferrer"
              className="text-sm text-[var(--accent)] underline"
            >
              {t('setup.watchmodeLink')}
            </a>
            <label className="block space-y-1 text-sm">
              <span className="text-[var(--muted)]">{t('settings.apiKey')}</span>
              <input
                type="password"
                autoComplete="off"
                disabled={watchmodeLocked}
                value={watchmodeKey}
                onChange={(e) => {
                  setWatchmodeKey(e.target.value)
                  setWatchmodeTest(null)
                }}
                placeholder={settings.data?.watchmode.apiKey.isSet ? t('settings.apiKeySet') : ''}
                className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2 disabled:opacity-60"
              />
              {watchmodeLocked && (
                <p className="text-xs text-[var(--muted)]">{t('setup.envLocked')}</p>
              )}
            </label>
            <div className="flex flex-wrap items-center gap-3">
              <button
                type="button"
                onClick={() => void runKeyTest('watchmode')}
                className="rounded-md bg-white/10 px-3 py-1.5 text-sm hover:bg-white/15"
              >
                {t('settings.testConnection')}
              </button>
              {watchmodeTest && <span className="text-sm text-[var(--muted)]">{watchmodeTest}</span>}
            </div>
            <div className="grid gap-3 sm:grid-cols-2">
              <label className="block space-y-1 text-sm">
                <span className="text-[var(--muted)]">{t('setup.watchmodePlan')}</span>
                <select
                  disabled={planLocked}
                  value={plan}
                  onChange={(e) => setPlan(e.target.value)}
                  className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2 disabled:opacity-60"
                >
                  <option value="free">{t('setup.planFree')}</option>
                  <option value="paid">{t('setup.planPaid')}</option>
                </select>
              </label>
              <label className="block space-y-1 text-sm">
                <span className="text-[var(--muted)]">{t('setup.watchmodeQuota')}</span>
                <input
                  type="number"
                  disabled={quotaLocked}
                  value={quota}
                  onChange={(e) => setQuota(Number(e.target.value))}
                  className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2 disabled:opacity-60"
                />
              </label>
              <label className="block space-y-1 text-sm">
                <span className="text-[var(--muted)]">{t('setup.watchmodeReserve')}</span>
                <input
                  type="number"
                  disabled={reserveLocked}
                  value={reservePercent}
                  onChange={(e) => setReservePercent(Number(e.target.value))}
                  className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2 disabled:opacity-60"
                />
              </label>
              <label className="block space-y-1 text-sm">
                <span className="text-[var(--muted)]">{t('setup.watchmodeMaxCredits')}</span>
                <input
                  type="number"
                  disabled={maxCreditsLocked}
                  value={maxCreditsPerRun}
                  onChange={(e) => setMaxCreditsPerRun(Number(e.target.value))}
                  className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2 disabled:opacity-60"
                />
              </label>
            </div>
          </section>

          <section className="space-y-3 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
            <h2 className="text-lg font-medium">{t('settings.tmdb')}</h2>
            <p className="text-sm text-[var(--muted)]">{t('setup.tmdbWhy')}</p>
            <a
              href="https://www.themoviedb.org/settings/api"
              target="_blank"
              rel="noreferrer"
              className="text-sm text-[var(--accent)] underline"
            >
              {t('setup.tmdbLink')}
            </a>
            <label className="block space-y-1 text-sm">
              <span className="text-[var(--muted)]">{t('settings.apiKey')}</span>
              <input
                type="password"
                autoComplete="off"
                disabled={tmdbLocked}
                value={tmdbKey}
                onChange={(e) => {
                  setTmdbKey(e.target.value)
                  setTmdbTest(null)
                }}
                placeholder={settings.data?.tmdb.apiKey.isSet ? t('settings.apiKeySet') : ''}
                className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2 disabled:opacity-60"
              />
              {tmdbLocked && <p className="text-xs text-[var(--muted)]">{t('setup.envLocked')}</p>}
            </label>
            <div className="flex flex-wrap items-center gap-3">
              <button
                type="button"
                onClick={() => void runKeyTest('tmdb')}
                className="rounded-md bg-white/10 px-3 py-1.5 text-sm hover:bg-white/15"
              >
                {t('settings.testConnection')}
              </button>
              {tmdbTest && <span className="text-sm text-[var(--muted)]">{tmdbTest}</span>}
            </div>
          </section>

          <section className="space-y-3 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
            <h2 className="text-lg font-medium">{t('settings.recommendations')}</h2>
            <label className="flex items-start gap-3 text-sm">
              <input
                type="checkbox"
                className="mt-1"
                disabled={recoLocked}
                checked={recoEnabled}
                onChange={(e) => setRecoEnabled(e.target.checked)}
              />
              <span>
                <span className="block">{t('settings.recoEnabled')}</span>
                <span className="block text-xs text-[var(--muted)]">{t('settings.recoEnabledHint')}</span>
              </span>
            </label>
          </section>
        </div>
      )}

      {step === 3 && (
        <CatalogSourcesForm ref={catalogsRef} showHeading={false} showSaveButton={false} />
      )}

      {step === 4 && (
        <div className="space-y-6">
          <p className="text-sm text-[var(--muted)]">{t('setup.arrOptional')}</p>
          <ArrSetupSection
            title={t('settings.sonarr')}
            form={sonarr}
            onChange={(next) => {
              setSonarr(next)
              setSonarrOk(false)
              setSonarrTest(null)
              setSonarrLists(null)
            }}
            apiKeySet={settings.data?.sonarr.apiKey.isSet ?? false}
            urlLocked={sonarrUrlLocked}
            keyLocked={sonarrKeyLocked}
            profiles={sonarrLists?.profiles ?? []}
            roots={sonarrLists?.roots ?? []}
            testMessage={sonarrTest}
            onTest={() => void runArrTest('sonarr')}
          />
          <ArrSetupSection
            title={t('settings.radarr')}
            form={radarr}
            onChange={(next) => {
              setRadarr(next)
              setRadarrOk(false)
              setRadarrTest(null)
              setRadarrLists(null)
            }}
            apiKeySet={settings.data?.radarr.apiKey.isSet ?? false}
            urlLocked={radarrUrlLocked}
            keyLocked={radarrKeyLocked}
            profiles={radarrLists?.profiles ?? []}
            roots={radarrLists?.roots ?? []}
            testMessage={radarrTest}
            onTest={() => void runArrTest('radarr')}
          />
        </div>
      )}

      {error && <p className="text-sm text-red-300">{error}</p>}

      <div className="flex flex-wrap items-center gap-3">
        {step > 1 && (
          <button
            type="button"
            disabled={busy}
            onClick={() => {
              setError(null)
              setStep((current) => current - 1)
            }}
            className="rounded-md bg-white/10 px-4 py-2 text-sm hover:bg-white/15 disabled:opacity-60"
          >
            {t('setup.back')}
          </button>
        )}
        <button
          type="button"
          disabled={busy || finish.isPending}
          onClick={() => void goNext()}
          className="rounded-md bg-[var(--accent)] px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
        >
          {step === STEPS ? t('setup.finish') : t('setup.next')}
        </button>
      </div>
    </div>
  )
}

function ArrSetupSection({
  title,
  form,
  onChange,
  apiKeySet,
  urlLocked,
  keyLocked,
  profiles,
  roots,
  testMessage,
  onTest,
}: {
  title: string
  form: ArrForm
  onChange: (value: ArrForm) => void
  apiKeySet: boolean
  urlLocked: boolean
  keyLocked: boolean
  profiles: Array<{ id: number; name: string }>
  roots: Array<{ path: string }>
  testMessage: string | null
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
          disabled={urlLocked}
          value={form.url}
          onChange={(e) => onChange({ ...form, url: e.target.value })}
          className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2 disabled:opacity-60"
          placeholder="http://localhost:8989"
        />
      </label>
      <label className="block space-y-1 text-sm">
        <span className="text-[var(--muted)]">{t('settings.apiKey')}</span>
        <input
          type="password"
          autoComplete="off"
          disabled={keyLocked}
          value={form.apiKey}
          onChange={(e) => onChange({ ...form, apiKey: e.target.value })}
          placeholder={apiKeySet ? t('settings.apiKeySet') : ''}
          className="w-full rounded-md border border-white/10 bg-[var(--surface)] px-3 py-2 disabled:opacity-60"
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
      <div className="flex flex-wrap items-center gap-3">
        <button
          type="button"
          onClick={onTest}
          className="rounded-md bg-white/10 px-3 py-1.5 text-sm hover:bg-white/15"
        >
          {t('settings.testConnection')}
        </button>
        {testMessage && <span className="text-sm text-[var(--muted)]">{testMessage}</span>}
      </div>
    </section>
  )
}
