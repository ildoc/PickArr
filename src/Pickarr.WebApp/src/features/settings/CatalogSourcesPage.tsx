import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../../api/client'
import {
  fetchCatalogSetup,
  updateCatalogSetup,
  type CatalogSetup,
} from '../../api/catalogSetup'
import { estimateCatalog } from './catalogEstimate'

type DraftProvider = {
  id: number
  enabled: boolean
  countries: string[]
}

const regionNames = new Intl.DisplayNames(['en'], { type: 'region' })

function draftFrom(setup: CatalogSetup): DraftProvider[] {
  return setup.providers.map((provider) => ({
    id: provider.id,
    enabled: provider.enabled,
    countries: provider.countries.filter((country) => country.enabled).map((country) => country.code),
  }))
}

export function CatalogSourcesPage() {
  const { t } = useTranslation()
  const queryClient = useQueryClient()
  const setup = useQuery({ queryKey: ['settings', 'catalogs'], queryFn: fetchCatalogSetup })
  const [draft, setDraft] = useState<DraftProvider[] | null>(null)
  const [notice, setNotice] = useState<string | null>(null)

  useEffect(() => {
    if (setup.data && draft === null) {
      setDraft(draftFrom(setup.data))
    }
  }, [draft, setup.data])

  const save = useMutation({
    mutationFn: updateCatalogSetup,
    onSuccess: async (next) => {
      queryClient.setQueryData(['settings', 'catalogs'], next)
      setDraft(draftFrom(next))
      setNotice(t('settings.catalogSaved'))
    },
    onError: (error) => {
      setNotice(error instanceof ApiError ? error.message : t('settings.catalogSaveFailed'))
    },
  })

  const estimate = useMemo(() => {
    if (!setup.data || !draft) return null
    const byId = new Map(draft.map((provider) => [provider.id, provider]))
    const countries = setup.data.providers.flatMap((provider) => {
      const selected = byId.get(provider.id)
      return provider.countries.map((country) => ({
        code: country.code,
        selected: Boolean(selected?.enabled && selected.countries.includes(country.code)),
        pages: country.pages,
        incrementalPages: country.incrementalPages,
        fullyImported: country.fullyImported,
      }))
    })
    return estimateCatalog({
      countries,
      used: setup.data.used,
      quota: setup.data.quota,
      freePlan: setup.data.plan !== 'paid',
      freeCountryLimit: setup.data.freeCountryLimit,
      creditsPerCall: setup.data.creditsPerCall,
      fullSyncsPerMonth: setup.data.fullSyncsPerMonth,
      dailyRunsPerMonth: setup.data.dailyRunsPerMonth,
    })
  }, [draft, setup.data])

  if (setup.isLoading || (setup.data && !draft)) {
    return <p className="text-[var(--muted)]">{t('admin.loading')}</p>
  }

  if (setup.isError || !setup.data || !draft || !estimate) {
    return <p className="text-red-300">{t('settings.catalogLoadFailed')}</p>
  }

  const missingCountry = draft.some((provider) => provider.enabled && provider.countries.length === 0)
  const blocked = missingCountry || estimate.overCountryLimit || estimate.overQuota
  const monthTotal = setup.data.used + estimate.monthly
  const ratio = Math.min(100, Math.round((monthTotal / Math.max(1, setup.data.quota)) * 100))

  function updateProvider(id: number, change: (provider: DraftProvider) => DraftProvider) {
    setNotice(null)
    setDraft((current) => current?.map((provider) => (provider.id === id ? change(provider) : provider)) ?? current)
  }

  return (
    <div className="grid gap-6 lg:grid-cols-[minmax(0,1fr)_22rem]">
      <div className="space-y-4">
        <p className="text-sm text-[var(--muted)]">{t('settings.catalogIntro')}</p>
        {setup.data.providers.map((provider) => {
          const selected = draft.find((item) => item.id === provider.id)
          if (!selected) return null
          return (
            <section key={provider.id} className="space-y-4 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
              <div className="flex items-center justify-between gap-4">
                <h2 className="text-lg font-medium">{provider.name}</h2>
                <button
                  type="button"
                  aria-pressed={selected.enabled}
                  onClick={() => updateProvider(provider.id, (item) => ({ ...item, enabled: !item.enabled }))}
                  className={`rounded-full px-3 py-1 text-sm ${
                    selected.enabled ? 'bg-emerald-400/20 text-emerald-200' : 'bg-white/10 text-[var(--muted)]'
                  }`}
                >
                  {selected.enabled ? t('settings.providerOn') : t('settings.providerOff')}
                </button>
              </div>
              <fieldset className="space-y-2" disabled={!selected.enabled}>
                <legend className="text-sm text-[var(--muted)]">{t('settings.countries')}</legend>
                <div className="grid gap-2 sm:grid-cols-2">
                  {provider.countries.map((country) => {
                    const checked = selected.countries.includes(country.code)
                    return (
                      <label key={country.code} className="flex items-start gap-2 text-sm">
                        <input
                          type="checkbox"
                          className="mt-1"
                          checked={checked}
                          onChange={() =>
                            updateProvider(provider.id, (item) => ({
                              ...item,
                              countries: checked
                                ? item.countries.filter((code) => code !== country.code)
                                : [...item.countries, country.code],
                            }))
                          }
                        />
                        <span>
                          <span className="block">{regionNames.of(country.code) ?? country.code}</span>
                          <span className="block text-xs text-[var(--muted)]">
                            {country.pagesMeasured
                              ? t('settings.pagesMeasured', { pages: country.pages })
                              : t('settings.pagesEstimated', { pages: country.pages })}
                          </span>
                        </span>
                      </label>
                    )
                  })}
                </div>
              </fieldset>
            </section>
          )
        })}
      </div>

      <aside className="h-fit space-y-3 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5 lg:sticky lg:top-6">
        <h2 className="text-lg font-medium">{t('settings.estimateHeading')}</h2>
        <p className="text-sm">
          {setup.data.plan === 'paid'
            ? t('settings.estimateCountriesPaid', { count: estimate.countries })
            : t('settings.estimateCountries', {
                count: estimate.countries,
                limit: setup.data.freeCountryLimit,
              })}
        </p>
        <p className="text-sm">{t('settings.estimateCatalogs', { count: estimate.catalogs })}</p>
        <p className="text-sm">{t('settings.estimateFill', { credits: estimate.fill })}</p>
        <p className="text-sm">{t('settings.estimateMonthly', { credits: estimate.monthly })}</p>
        <p className="text-sm">
          {t('settings.estimateUsed', {
            used: setup.data.used,
            total: monthTotal,
            quota: setup.data.quota,
          })}
        </p>
        <div className="h-2 overflow-hidden rounded-full bg-white/10">
          <div
            className={`h-full ${estimate.overQuota ? 'bg-red-400' : 'bg-emerald-400'}`}
            style={{ width: `${ratio}%` }}
          />
        </div>
        <p className="text-xs text-[var(--muted)]">{t('settings.estimateNote')}</p>
        {missingCountry && <p className="text-sm text-amber-200">{t('settings.estimateNeedCountry')}</p>}
        {estimate.overCountryLimit && (
          <p className="text-sm text-amber-200">
            {t('settings.estimateOverCountries', { limit: setup.data.freeCountryLimit })}
          </p>
        )}
        {estimate.overQuota && (
          <p className="text-sm text-amber-200">{t('settings.estimateOverQuota', { quota: setup.data.quota })}</p>
        )}
        {notice && <p className="text-sm text-[var(--muted)]">{notice}</p>}
        <button
          type="button"
          disabled={blocked || save.isPending}
          onClick={() =>
            save.mutate({
              providers: draft.map((provider) => ({
                id: provider.id,
                enabled: provider.enabled,
                countries: provider.countries,
              })),
            })
          }
          className="rounded-md bg-[var(--accent)] px-4 py-2 text-sm font-medium text-white disabled:opacity-60"
        >
          {t('settings.save')}
        </button>
      </aside>
    </div>
  )
}
