import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'
import type { TitleListItem } from '../../api/types'
import { TitleCard } from './TitleCard'

export function TitleGrid({
  items,
  hasMore,
  isFetchingNextPage,
  onLoadMore,
  showPlaces = false,
  dismissOnRate = false,
}: {
  items: TitleListItem[]
  hasMore: boolean
  isFetchingNextPage: boolean
  onLoadMore: () => void
  showPlaces?: boolean
  dismissOnRate?: boolean
}) {
  const { t } = useTranslation()
  const sentinel = useRef<HTMLDivElement | null>(null)

  useEffect(() => {
    const node = sentinel.current
    if (!node || !hasMore) return
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((e) => e.isIntersecting)) onLoadMore()
      },
      { rootMargin: '400px' },
    )
    observer.observe(node)
    return () => observer.disconnect()
  }, [hasMore, onLoadMore])

  return (
    <div>
      <div className="grid grid-cols-[repeat(auto-fill,minmax(180px,1fr))] gap-4">
        {items.map((title) => (
          <TitleCard
            key={title.id}
            title={title}
            showPlaces={showPlaces}
            dismissOnRate={dismissOnRate}
          />
        ))}
      </div>
      <div ref={sentinel} className="h-8" />
      {isFetchingNextPage && (
        <p className="py-4 text-center text-sm text-[var(--muted)]">{t('catalog.loading')}</p>
      )}
      {hasMore && !isFetchingNextPage && (
        <div className="flex justify-center py-2">
          <button
            type="button"
            onClick={onLoadMore}
            className="rounded-md bg-white/5 px-4 py-2 text-sm text-[var(--muted)] hover:bg-white/10"
          >
            {t('catalog.loadMore')}
          </button>
        </div>
      )}
    </div>
  )
}
