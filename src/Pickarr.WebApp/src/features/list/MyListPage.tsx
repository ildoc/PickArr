import { keepPreviousData, useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { useCallback, useEffect } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { fetchMyList, fetchMyListGenres, fetchMyListTypes } from '../../api/favorites'
import { useDebouncedValue } from '../../lib/useDebouncedValue'
import { FilterBar } from '../catalog/FilterBar'
import { TitleGrid } from '../catalog/TitleGrid'
import { TitleDialog } from '../title/TitleDialog'

export function MyListPage() {
  const { t } = useTranslation()
  const [params, setParams] = useSearchParams()

  const q = params.get('q') ?? ''
  const type = params.get('type') ?? ''
  const genre = params.get('genre') ?? ''
  const sort = params.get('sort') ?? (q ? 'relevance' : 'added')
  const dirParam = params.get('dir')
  const dir: 'asc' | 'desc' =
    dirParam === 'asc' || dirParam === 'desc' ? dirParam : sort === 'title' ? 'asc' : 'desc'
  const titleParam = params.get('title')
  const titleId = titleParam ? Number(titleParam) : null

  const debouncedQ = useDebouncedValue(q, 300)

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
    queryKey: ['my-list', 'types'],
    queryFn: fetchMyListTypes,
  })

  const genresQuery = useQuery({
    queryKey: ['my-list', 'genres'],
    queryFn: fetchMyListGenres,
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
    queryKey: ['my-list', 'titles', debouncedQ, type, genre, sort, dir],
    initialPageParam: 1,
    placeholderData: keepPreviousData,
    queryFn: ({ pageParam }) =>
      fetchMyList({
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
      <p className="text-sm text-[var(--muted)]">{t('favorite.hint')}</p>
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
        sources={[]}
        countrySourceId={null}
        onCountryChange={() => undefined}
        type={type}
        onTypeChange={(value) => patchParams({ type: value || null })}
        types={typesQuery.data ?? []}
        genre={genre}
        genres={genresQuery.data ?? []}
        onGenreChange={(value) => patchParams({ genre: value || null })}
        hideSource
        hideViews
      />

      {titlesQuery.isLoading && <p className="text-[var(--muted)]">{t('catalog.loading')}</p>}

      {!emptyReason && !titlesQuery.isLoading && items.length === 0 && (
        <p className="text-[var(--muted)]">{t('catalog.emptyResults')}</p>
      )}

      {!emptyReason && items.length > 0 && (
        <TitleGrid
          items={items}
          hasMore={Boolean(titlesQuery.hasNextPage)}
          isFetchingNextPage={titlesQuery.isFetchingNextPage}
          onLoadMore={onLoadMore}
          showPlaces
        />
      )}

      {titleId != null && Number.isFinite(titleId) && (
        <TitleDialog titleId={titleId} sourceId={null} />
      )}
    </div>
  )
}
