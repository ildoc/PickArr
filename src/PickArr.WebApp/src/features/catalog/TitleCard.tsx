import { useEffect, useState } from 'react'
import { Link, useLocation, useSearchParams } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import type { TitleListItem } from '../../api/types'
import { categoryLabelKey, formatYear } from '../../lib/format'
import { FavoriteButton } from '../favorites/FavoriteButton'
import { RatingButtons } from '../ratings/RatingButtons'
import { RECO_DISMISS_FADE_MS, RECO_DISMISS_HOLD_MS } from '../ratings/ratingCache'
import { ReasonChips } from '../reco/ReasonChips'
import { ArrStatusBadge } from '../title/ArrStatusBadge'

export function TitleCard({
  title,
  showPlaces = false,
  dismissOnRate = false,
}: {
  title: TitleListItem
  showPlaces?: boolean
  /** Fade the card out after a vote on For you / recommended rows. */
  dismissOnRate?: boolean
}) {
  const { t } = useTranslation()
  const { pathname } = useLocation()
  const [params] = useSearchParams()
  const detailParams = new URLSearchParams(params)
  detailParams.set('title', String(title.id))
  const [exiting, setExiting] = useState(false)

  useEffect(() => {
    if (!dismissOnRate || title.rating == null) {
      setExiting(false)
      return
    }
    const timer = window.setTimeout(() => setExiting(true), RECO_DISMISS_HOLD_MS)
    return () => window.clearTimeout(timer)
  }, [dismissOnRate, title.rating])

  return (
    <Link
      to={`${pathname}?${detailParams.toString()}`}
      aria-hidden={exiting || undefined}
      tabIndex={exiting ? -1 : undefined}
      className={`group flex flex-col overflow-hidden rounded-lg bg-[var(--surface-2)] outline-none ring-[var(--accent)] transition-[opacity,transform] hover:ring-2 focus-visible:ring-2 ${
        exiting
          ? 'pointer-events-none scale-95 opacity-0'
          : 'scale-100 opacity-100'
      }`}
      style={{ transitionDuration: `${RECO_DISMISS_FADE_MS}ms` }}
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
        <div className="absolute right-1 top-1 flex items-center gap-0.5 rounded-full bg-black/55">
          <RatingButtons titleId={title.id} rating={title.rating ?? null} />
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
        {title.sourceStatus && (
          <p className="text-xs text-[var(--muted)]">
            {title.sourceStatus === 'present'
              ? t('ratings.inSource')
              : title.sourceStatus === 'removed'
                ? t('ratings.removedFromSource')
                : t('ratings.absentFromSource')}
          </p>
        )}
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
        {title.reco?.reasons && title.reco.reasons.length > 0 && (
          <ReasonChips reasons={title.reco.reasons} />
        )}
      </div>
    </Link>
  )
}
