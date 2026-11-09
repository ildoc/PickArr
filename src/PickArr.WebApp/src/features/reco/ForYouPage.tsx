import { keepPreviousData, useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { fetchCatalogTypes, fetchGenres, fetchSources } from '../../api/catalog'
import { fetchRecommendations, fetchRecoStatus } from '../../api/recommendations'
import { fetchSettings } from '../../api/settings'
import { loadPreferredSourceId, savePreferredSourceId } from '../../lib/sourcePreference'
import { FilterBar } from '../catalog/FilterBar'
import { TitleGrid } from '../catalog/TitleGrid'
import { TitleDialog } from '../title/TitleDialog'
import { LearningProgress } from './LearningProgress'

export function ForYouPage() {
  const { t } = useTranslation()
  const [params, setParams] = useSearchParams()

  const type = params.get('type') ?? ''
  const genre = params.get('genre') ?? ''
  // Per-source picks live on /catalog; this page is cross-provider.
  const scope = 'enabled' as const
  const includeInLibrary = params.get('includeInLibrary') === '1'
  const sourceParam = params.get('source')
  const titleParam = params.get('title')
  const titleId = titleParam ? Number(titleParam) : null

  const settings = useQuery({ queryKey: ['settings'], queryFn: fetchSettings })
  const recoEnabled = settings.data?.reco.enabled !== false

  const sourcesQuery = useQuery({
    queryKey: ['sources'],
    queryFn: fetchSources,
  })

  const sourceId = useMemo(() => {
    const list = sourcesQuery.data ?? []
    if (list.length === 0) return null
    if (sourceParam) {
      const id = Number(sourceParam)
      if (list.some((s) => s.id === id)) return id
    }
    const preferred = loadPreferredSourceId()
    if (preferred && list.some((s) => s.id === preferred)) return preferred
    return list[0]!.id
  }, [sourcesQuery.data, sourceParam])

  useEffect(() => {
    if (sourceId == null) return
    savePreferredSourceId(sourceId)
    if (sourceParam !== String(sourceId)) {
      const next = new URLSearchParams(params)
      next.set('source', String(sourceId))
      setParams(next, { replace: true })
    }
  }, [sourceId, sourceParam, params, setParams])

  const patchParams = useCallback(
    (patch: Record<string, string | null>) => {
      const next = new URLSearchParams(params)
      for (const [key, value] of Object.entries(patch)) {
        if (value == null || value === '') next.delete(key)
        else next.set(key, value)
      }
      setParams(next, { replace: true })
    },
    [params, setParams],
  )

  const typesQuery = useQuery({
    queryKey: ['catalog-types', sourceId],
    enabled: sourceId != null,
    queryFn: () => fetchCatalogTypes(sourceId!),
  })

  const genresQuery = useQuery({
    queryKey: ['catalog-genres', sourceId],
    enabled: sourceId != null,
    queryFn: () => fetchGenres(sourceId!),
  })

  useEffect(() => {
    if (!type || !typesQuery.data) return
    if (!typesQuery.data.some((item) => item.type === type)) {
      patchParams({ type: null })
    }
  }, [type, typesQuery.data, patchParams])

  useEffect(() => {
    if (!genre || !genresQuery.data) return
    if (!genresQuery.data.some((item) => String(item.id) === genre)) {
      patchParams({ genre: null })
    }
  }, [genre, genresQuery.data, patchParams])

  const statusQuery = useQuery({
    queryKey: ['reco-status'],
    queryFn: fetchRecoStatus,
    enabled: recoEnabled,
  })

  const recoQuery = useInfiniteQuery({
    queryKey: ['recommendations', scope, sourceId, type, genre, includeInLibrary],
    enabled: recoEnabled,
    initialPageParam: 1,
    placeholderData: keepPreviousData,
    queryFn: ({ pageParam }) =>
      fetchRecommendations({
        scope,
        sourceId: sourceId ?? undefined,
        type: type || undefined,
        genre: genre || undefined,
        includeInLibrary,
        page: pageParam,
        pageSize: 48,
      }),
    getNextPageParam: (last) =>
      last.page * last.pageSize < last.total ? last.page + 1 : undefined,
  })

  const firstPage = recoQuery.data?.pages[0]
  const items = recoQuery.data?.pages.flatMap((p) => p.items) ?? []
  const total = firstPage?.total

  const onLoadMore = useCallback(() => {
    if (recoQuery.hasNextPage && !recoQuery.isFetchingNextPage) {
      void recoQuery.fetchNextPage()
    }
  }, [recoQuery])

  if (settings.data && !settings.data.reco.enabled) {
    return <p className="text-[var(--muted)]">{t('reco.disabled')}</p>
  }

  return (
    <div className="space-y-6">
      {statusQuery.data && <LearningProgress status={statusQuery.data} />}

      <p className="text-sm text-[var(--muted)]">{t('reco.enabledScopeIntro')}</p>

      <div className="flex flex-wrap items-center gap-3">
        <label className="flex items-center gap-2 text-sm text-[var(--muted)]">
          <input
            type="checkbox"
            checked={includeInLibrary}
            onChange={(e) => patchParams({ includeInLibrary: e.target.checked ? '1' : null })}
          />
          {t('reco.includeInLibrary')}
        </label>
      </div>

      <FilterBar
        q=""
        onQChange={() => undefined}
        view="all"
        onViewChange={() => undefined}
        sort="added"
        onSortChange={() => undefined}
        dir="desc"
        onDirChange={() => undefined}
        total={total}
        sources={sourcesQuery.data ?? []}
        countrySourceId={sourceId}
        onCountryChange={(id) => patchParams({ source: String(id) })}
        type={type}
        onTypeChange={(value) => patchParams({ type: value || null })}
        types={typesQuery.data ?? []}
        genre={genre}
        genres={genresQuery.data ?? []}
        onGenreChange={(value) => patchParams({ genre: value || null })}
        hideViews
        hideSearch
        hideSort
      />

      {recoQuery.isLoading && <p className="text-[var(--muted)]">{t('catalog.loading')}</p>}

      {recoQuery.isError && (
        <div className="rounded-xl border border-amber-500/30 bg-amber-950/40 p-6 text-amber-100">
          <p>{t('reco.loadError')}</p>
          <Link to="/settings" className="mt-3 inline-block underline">
            {t('nav.settings')}
          </Link>
        </div>
      )}

      {!recoQuery.isLoading && !recoQuery.isError && items.length === 0 && (
        <p className="text-[var(--muted)]">{t('reco.empty')}</p>
      )}

      {items.length > 0 && (
        <TitleGrid
          items={items}
          hasMore={Boolean(recoQuery.hasNextPage)}
          isFetchingNextPage={recoQuery.isFetchingNextPage}
          onLoadMore={onLoadMore}
          showPlaces
        />
      )}

      {titleId != null && Number.isFinite(titleId) && (
        <TitleDialog titleId={titleId} sourceId={sourceId} />
      )}
    </div>
  )
}
