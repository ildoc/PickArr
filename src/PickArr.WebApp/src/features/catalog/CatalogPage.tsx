import { keepPreviousData, useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { fetchCatalogTypes, fetchGenres, fetchSources, fetchTitles } from '../../api/catalog'
import { loadPreferredSourceId, savePreferredSourceId } from '../../lib/sourcePreference'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { CatalogForYouRow } from '../reco/CatalogForYouRow'
import { TitleDialog } from '../title/TitleDialog'
import { FilterBar } from './FilterBar'
import { TitleGrid } from './TitleGrid'

export function CatalogPage() {
  const { t } = useTranslation()
  const [params, setParams] = useSearchParams()

  const q = params.get('q') ?? ''
  const view = params.get('view') ?? 'all'
  const type = params.get('type') ?? ''
  const genre = params.get('genre') ?? ''
  const sort = params.get('sort') ?? (q ? 'relevance' : 'added')
  const dirParam = params.get('dir')
  const dir: 'asc' | 'desc' =
    dirParam === 'asc' || dirParam === 'desc' ? dirParam : sort === 'title' ? 'asc' : 'desc'
  const sourceParam = params.get('source')
  const titleParam = params.get('title')
  const titleId = titleParam ? Number(titleParam) : null

  const debouncedQ = useDebouncedValue(q, 300)

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

  const titlesQuery = useInfiniteQuery({
    queryKey: ['titles', sourceId, debouncedQ, view, type, genre, sort, dir],
    enabled: sourceId != null,
    initialPageParam: 1,
    placeholderData: keepPreviousData,
    queryFn: ({ pageParam }) =>
      fetchTitles(sourceId!, {
        q: debouncedQ || undefined,
        view,
        type: type || undefined,
        genre: genre || undefined,
        includeRemoved: view === 'removed' || undefined,
        sort,
        dir,
        page: pageParam,
        pageSize: 48,
      }),
    getNextPageParam: (last) =>
      last.page * last.pageSize < last.total ? last.page + 1 : undefined,
  })

  const firstPage = titlesQuery.data?.pages[0]
  const emptyReason = firstPage?.emptyReason
  const items = titlesQuery.data?.pages.flatMap((p) => p.items) ?? []
  const total = firstPage?.total

  const onLoadMore = useCallback(() => {
    if (titlesQuery.hasNextPage && !titlesQuery.isFetchingNextPage) {
      void titlesQuery.fetchNextPage()
    }
  }, [titlesQuery])

  return (
    <div className="space-y-6">
      <FilterBar
        q={q}
        onQChange={(value) =>
          patchParams({
            q: value || null,
            sort: value ? 'relevance' : 'added',
            dir: 'desc',
          })
        }
        view={view}
        onViewChange={(v) => patchParams({ view: v === 'all' ? null : v })}
        sort={sort}
        onSortChange={(s) => patchParams({ sort: s, dir: s === 'title' ? 'asc' : 'desc' })}
        dir={dir}
        onDirChange={(value) => patchParams({ dir: value })}
        total={emptyReason ? 0 : total}
        sources={sourcesQuery.data ?? []}
        countrySourceId={sourceId}
        onCountryChange={(id) => patchParams({ source: String(id) })}
        type={type}
        onTypeChange={(value) => patchParams({ type: value || null })}
        types={typesQuery.data ?? []}
        genre={genre}
        genres={genresQuery.data ?? []}
        onGenreChange={(value) => patchParams({ genre: value || null })}
      />

      {titlesQuery.isLoading && (
        <p className="text-[var(--muted)]">{t('catalog.loading')}</p>
      )}

      {emptyReason === 'arrNotConfigured' && (
        <div className="rounded-xl border border-amber-500/30 bg-amber-950/40 p-6 text-amber-100">
          <p>{t('catalog.emptyArr')}</p>
          <Link to="/settings" className="mt-3 inline-block underline">
            {t('nav.settings')}
          </Link>
        </div>
      )}

      {emptyReason === 'catalogNotImported' && (
        <div className="rounded-xl border border-white/10 bg-[var(--surface-2)] p-6">
          <p className="text-[var(--muted)]">{t('catalog.emptyNotImported')}</p>
          <Link to="/settings/jobs" className="mt-3 inline-block underline">
            {t('nav.jobs')}
          </Link>
        </div>
      )}

      {emptyReason === 'catalogImporting' && (
        <p className="text-[var(--muted)]">{t('catalog.emptyImporting')}</p>
      )}

      {!emptyReason && view === 'leaving' && (
        <p className="text-sm text-[var(--muted)]">{t('catalog.leavingFreeNote')}</p>
      )}

      {!emptyReason && sourceId != null && view === 'all' && !debouncedQ && (
        <CatalogForYouRow sourceId={sourceId} />
      )}

      {!emptyReason && !titlesQuery.isLoading && items.length === 0 && (
        <p className="text-[var(--muted)]">
          {view === 'leaving' ? t('catalog.emptyLeaving') : t('catalog.emptyResults')}
        </p>
      )}

      {!emptyReason && items.length > 0 && (
        <TitleGrid
          items={items}
          hasMore={Boolean(titlesQuery.hasNextPage)}
          isFetchingNextPage={titlesQuery.isFetchingNextPage}
          onLoadMore={onLoadMore}
        />
      )}

      {titleId != null && Number.isFinite(titleId) && (
        <TitleDialog titleId={titleId} sourceId={sourceId} />
      )}
    </div>
  )
}
