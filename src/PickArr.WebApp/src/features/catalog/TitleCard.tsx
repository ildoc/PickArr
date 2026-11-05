import { Link, useLocation, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import type { TitleListItem } from '../../api/types'
import { categoryLabelKey, formatYear } from '../../lib/format'
import { FavoriteButton } from '../favorites/FavoriteButton'
import { ArrStatusBadge } from '../title/ArrStatusBadge'

export function TitleCard({
  title,
  showPlaces = false,
}: {
  title: TitleListItem
  showPlaces?: boolean
}) {
  const { t } = useTranslation()
  const { pathname } = useLocation()
  const [params] = useSearchParams()
  const detailParams = new URLSearchParams(params)
  detailParams.set('title', String(title.id))

  return (
    <Link
      to={`${pathname}?${detailParams.toString()}`}
      className="group flex flex-col overflow-hidden rounded-lg bg-[var(--surface-2)] outline-none ring-[var(--accent)] transition hover:ring-2 focus-visible:ring-2"
    >
      <div className="relative aspect-[2/3] bg-black/40">
        {title.posterUrl ? (
          <img
            src={title.posterUrl}
            alt=""
            loading="lazy"
            className="h-full w-full object-cover"
          />
        ) : (
          <div className="flex h-full items-center justify-center p-4 text-center text-sm text-[var(--muted)]">
            {title.name}
          </div>
        )}
        <div className="absolute right-1 top-1">
          <FavoriteButton titleId={title.id} isFavorite={title.isFavorite} />
        </div>
        <div className="absolute bottom-1 left-1 flex max-w-[70%] flex-wrap gap-1">
          <span className="rounded bg-black/70 px-1.5 py-0.5 text-[10px] uppercase tracking-wide">
            {t(categoryLabelKey(title.category))}
          </span>
          {title.arr && (
            <ArrStatusBadge state={title.arr.state} progress={title.arr.progress} />
          )}
        </div>
        {title.ratingTmdb != null && (
          <span className="absolute bottom-1 right-1 rounded bg-black/75 px-1.5 py-0.5 text-[11px] font-medium tabular-nums">
            {title.ratingTmdb.toFixed(1)}
          </span>
        )}
      </div>
      <div className="space-y-0.5 p-2.5">
        <h3 className="line-clamp-2 text-sm font-medium leading-snug text-[var(--text)]">
          {title.name}
        </h3>
        <p className="text-xs text-[var(--muted)]">{formatYear(title.year)}</p>
        {showPlaces && title.places && title.places.length > 0 && (
          <p className="text-xs text-[var(--muted)]">
            {title.places
              .map(
                (place) =>
                  `${place.providerName} · ${t(`country.${place.countryCode}`, { defaultValue: place.countryCode })}`,
              )
              .join(', ')}
          </p>
        )}
      </div>
    </Link>
  )
}
