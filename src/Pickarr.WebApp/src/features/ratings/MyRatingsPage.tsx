import { keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useRef } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { fetchSources } from '../../api/catalog'
import {
  exportRatings,
  fetchRatings,
  importRatings,
  type RatingValue,
} from '../../api/ratings'
import { fetchSettings } from '../../api/settings'
import type { CatalogSource, TitleListItem } from '../../api/types'
import { savePreferredSourceId } from '../../lib/sourcePreference'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { useProfiles } from '../who/profileContext'
import { FilterBar } from '../catalog/FilterBar'
import { TitleGrid } from '../catalog/TitleGrid'
import { TitleDialog } from '../title/TitleDialog'

export function MyRatingsPage() {
  const { t } = useTranslation()
  const { active } = useProfiles()
  const queryClient = useQueryClient()
  const fileRef = useRef<HTMLInputElement>(null)
  const [params, setParams] = useSearchParams()

  const q = params.get('q') ?? ''
  const type = params.get('type') ?? ''
  const genre = params.get('genre') ?? ''
  const tabParam = params.get('tab')
  const tab: RatingValue =
    tabParam === 'dislike'
      ? 'dislike'
      : tabParam === 'not_interested'
        ? 'not_interested'
        : tabParam === 'love'
          ? 'love'
          : 'like'
  const sort = params.get('sort') ?? (q ? 'relevance' : 'added')
  const dirParam = params.get('dir')
  const dir: 'asc' | 'desc' =
    dirParam === 'asc' || dirParam === 'desc' ? dirParam : sort === 'title' ? 'asc' : 'desc'
  const sourceParam = params.get('source')
  const titleParam = params.get('title')
  const titleId = titleParam ? Number(titleParam) : null
  const debouncedQ = useDebouncedValue(q, 300)

  const settings = useQuery({ queryKey: ['settings'], queryFn: fetchSettings })
  const sourcesQuery = useQuery({ queryKey: ['sources'], queryFn: fetchSources })

  // Default All: ratings stay global; a concrete source only drives presence badges.
  const sourceId = useMemo(() => {
    const list = sourcesQuery.data ?? []
    if (list.length === 0 || !sourceParam) return null
    const id = Number(sourceParam)
    return list.some((s) => s.id === id) ? id : null
  }, [sourcesQuery.data, sourceParam])

  useEffect(() => {
    if (!active || sourceId == null) return
    savePreferredSourceId(active.id, sourceId)
  }, [active, sourceId])

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

  const selectedSource = sourcesQuery.data?.find((s) => s.id === sourceId) ?? null

  const ratingsQuery = useInfiniteQuery({
    queryKey: ['ratings', tab, debouncedQ, type, genre, sort, dir],
    enabled: settings.data?.reco.enabled !== false,
    initialPageParam: 1,
    placeholderData: keepPreviousData,
    queryFn: ({ pageParam }) =>
      fetchRatings({
        value: tab,
        q: debouncedQ || undefined,
        type: type || undefined,
        genre: genre || undefined,
        sort,
        dir,
        page: pageParam,
        pageSize: 48,
      }),
    getNextPageParam: (last) =>
      last.page * last.pageSize < last.total ? last.page + 1 : undefined,
  })

  const firstPage = ratingsQuery.data?.pages[0]
  const emptyReason = firstPage?.emptyReason
  const items = useMemo(() => {
    const raw = ratingsQuery.data?.pages.flatMap((p) => p.items) ?? []
    return raw.map((item) => withSourceStatus(item, selectedSource))
  }, [ratingsQuery.data, selectedSource])
  const total = firstPage?.total

  const onLoadMore = useCallback(() => {
    if (ratingsQuery.hasNextPage && !ratingsQuery.isFetchingNextPage) {
      void ratingsQuery.fetchNextPage()
    }
  }, [ratingsQuery])

  const exportMutation = useMutation({
    mutationFn: exportRatings,
    onSuccess: (data) => {
      const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' })
      const url = URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = 'pickarr-ratings.json'
      a.click()
      URL.revokeObjectURL(url)
    },
  })

  const importMutation = useMutation({
    mutationFn: importRatings,
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['ratings'] })
      await queryClient.invalidateQueries({ queryKey: ['recommendations'] })
      await queryClient.invalidateQueries({ queryKey: ['reco-status'] })
      await queryClient.invalidateQueries({ queryKey: ['titles'] })
    },
  })

  const onImportFile = async (file: File) => {
    const text = await file.text()
    const parsed = JSON.parse(text) as {
      mode?: string
      items?: Array<{ tmdbType: string; tmdbId: number; value: RatingValue }>
    } | Array<{ tmdbType: string; tmdbId: number; value: RatingValue }>
    const itemsPayload = Array.isArray(parsed) ? parsed : (parsed.items ?? [])
    const mode = !Array.isArray(parsed) && parsed.mode === 'replace' ? 'replace' : 'merge'
    importMutation.mutate({ mode, items: itemsPayload })
  }

  if (settings.data && !settings.data.reco.enabled) {
    return <p className="text-[var(--muted)]">{t('ratings.disabled')}</p>
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex items-center gap-2">
          <button
            type="button"
            aria-pressed={tab === 'love'}
            onClick={() => patchParams({ tab: 'love' })}
            className={`rounded-md px-3 py-1.5 text-sm ${
              tab === 'love' ? 'bg-white/15 text-[var(--text)]' : 'text-[var(--muted)] hover:bg-white/5'
            }`}
          >
            {t('ratings.tabLove')}
          </button>
          <button
            type="button"
            aria-pressed={tab === 'like'}
            onClick={() => patchParams({ tab: 'like' })}
            className={`rounded-md px-3 py-1.5 text-sm ${
              tab === 'like' ? 'bg-white/15 text-[var(--text)]' : 'text-[var(--muted)] hover:bg-white/5'
            }`}
          >
            {t('ratings.tabLike')}
          </button>
          <button
            type="button"
            aria-pressed={tab === 'dislike'}
            onClick={() => patchParams({ tab: 'dislike' })}
            className={`rounded-md px-3 py-1.5 text-sm ${
              tab === 'dislike'
                ? 'bg-white/15 text-[var(--text)]'
                : 'text-[var(--muted)] hover:bg-white/5'
            }`}
          >
            {t('ratings.tabDislike')}
          </button>
          <button
            type="button"
            aria-pressed={tab === 'not_interested'}
            onClick={() => patchParams({ tab: 'not_interested' })}
            className={`rounded-md px-3 py-1.5 text-sm ${
              tab === 'not_interested'
                ? 'bg-white/15 text-[var(--text)]'
                : 'text-[var(--muted)] hover:bg-white/5'
            }`}
          >
            {t('ratings.tabNotInterested')}
          </button>
        </div>
        <div className="flex items-center gap-2">
          <button
            type="button"
            onClick={() => exportMutation.mutate()}
            disabled={exportMutation.isPending}
            className="rounded-md bg-white/10 px-3 py-1.5 text-sm hover:bg-white/15"
          >
            {t('ratings.export')}
          </button>
          <button
            type="button"
            onClick={() => fileRef.current?.click()}
            disabled={importMutation.isPending}
            className="rounded-md bg-white/10 px-3 py-1.5 text-sm hover:bg-white/15"
          >
            {t('ratings.import')}
          </button>
          <input
            ref={fileRef}
            type="file"
            accept="application/json,.json"
            className="hidden"
            onChange={(e) => {
              const file = e.target.files?.[0]
              if (file) void onImportFile(file)
              e.target.value = ''
            }}
          />
        </div>
      </div>

      {importMutation.isSuccess && importMutation.data && (
        <p className="text-sm text-emerald-300">
          {t('ratings.importDone', importMutation.data)}
        </p>
      )}
      {importMutation.isError && (
        <p className="text-sm text-rose-300">{t('ratings.importFailed')}</p>
      )}

      <FilterBar
        q={q}
        onQChange={(value) =>
          patchParams({
            q: value || null,
            sort: value ? 'relevance' : 'added',
            dir: 'desc',
          })
        }
        view="all"
        onViewChange={() => undefined}
        sort={sort}
        onSortChange={(s) => patchParams({ sort: s, dir: s === 'title' ? 'asc' : 'desc' })}
        dir={dir}
        onDirChange={(value) => patchParams({ dir: value })}
        total={emptyReason ? 0 : total}
        sources={sourcesQuery.data ?? []}
        countrySourceId={sourceId}
        onCountryChange={(id) => patchParams({ source: id == null ? null : String(id) })}
        type={type}
        onTypeChange={(value) => patchParams({ type: value || null })}
        types={[]}
        genre={genre}
        genres={[]}
        onGenreChange={(value) => patchParams({ genre: value || null })}
        hideViews
        allowAllSources
      />

      <p className="text-sm text-[var(--muted)]">{t('ratings.crossCatalog')}</p>

      {ratingsQuery.isLoading && <p className="text-[var(--muted)]">{t('catalog.loading')}</p>}

      {!emptyReason && !ratingsQuery.isLoading && items.length === 0 && (
        <p className="text-[var(--muted)]">{t('ratings.empty')}</p>
      )}

      {!emptyReason && items.length > 0 && (
        <TitleGrid
          items={items}
          hasMore={Boolean(ratingsQuery.hasNextPage)}
          isFetchingNextPage={ratingsQuery.isFetchingNextPage}
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

function withSourceStatus(
  item: TitleListItem,
  source: CatalogSource | null,
): TitleListItem {
  if (!source) return item
  const place = item.places?.find(
    (p) => p.providerName === source.providerName && p.countryCode === source.countryCode,
  )
  const sourceStatus: TitleListItem['sourceStatus'] = !place
    ? 'absent'
    : place.removedAt
      ? 'removed'
      : 'present'
  return { ...item, sourceStatus }
}
