import { useQuery } from '@tanstack/react-query'
import type { SyntheticEvent } from 'react'
import { useTranslation } from 'react-i18next'
import type { RatingValue } from '../../api/ratings'
import { fetchSettings } from '../../api/settings'
import type { UserRating } from './ratingCache'
import { useRatingMutation } from './useRatingMutation'

function stopCardNavigation(e: SyntheticEvent) {
  e.preventDefault()
  e.stopPropagation()
}

const compactActive: Record<RatingValue, string> = {
  love: 'bg-amber-500 text-white hover:bg-amber-400',
  like: 'bg-emerald-600 text-white hover:bg-emerald-500',
  dislike: 'bg-rose-600 text-white hover:bg-rose-500',
  not_interested: 'bg-slate-500 text-white hover:bg-slate-400',
}

const dialogActive: Record<RatingValue, string> = {
  love: 'bg-amber-500 text-white hover:bg-amber-400',
  like: 'bg-emerald-600 text-white hover:bg-emerald-500',
  dislike: 'bg-rose-600 text-white hover:bg-rose-500',
  not_interested: 'bg-slate-500 text-white hover:bg-slate-400',
}

export function RatingButtons({
  titleId,
  rating,
  variant = 'compact',
  className = '',
}: {
  titleId: number
  rating: UserRating
  variant?: 'compact' | 'dialog'
  className?: string
}) {
  const { t } = useTranslation()
  const settings = useQuery({ queryKey: ['settings'], queryFn: fetchSettings })
  const mutation = useRatingMutation(titleId)

  if (settings.data && !settings.data.reco.enabled) {
    return null
  }

  const toggle = (value: RatingValue) => {
    const next: UserRating = rating === value ? null : value
    mutation.mutate(next)
  }

  if (variant === 'dialog') {
    return (
      <div className={`flex flex-wrap items-center gap-2 ${className}`}>
        {(
          [
            ['love', t('ratings.love')],
            ['like', t('ratings.like')],
            ['dislike', t('ratings.dislike')],
            ['not_interested', t('ratings.notInterested')],
          ] as const
        ).map(([value, label]) => (
          <button
            key={value}
            type="button"
            aria-pressed={rating === value}
            aria-busy={mutation.isPending}
            onClick={() => toggle(value)}
            className={`rounded-md px-3 py-1.5 text-sm font-medium transition ${
              rating === value
                ? dialogActive[value]
                : 'bg-white/10 text-[var(--text)] hover:bg-white/15'
            }`}
          >
            {label}
          </button>
        ))}
      </div>
    )
  }

  return (
    <div
      className={`flex items-center gap-0.5 ${className}`}
      onClick={stopCardNavigation}
      onPointerDown={stopCardNavigation}
      onKeyDown={(e) => e.stopPropagation()}
    >
      {(
        [
          ['love', t('ratings.love'), '★'],
          ['like', t('ratings.like'), '▲'],
          ['dislike', t('ratings.dislike'), '▼'],
          ['not_interested', t('ratings.notInterested'), '✕'],
        ] as const
      ).map(([value, label, symbol]) => (
        <button
          key={value}
          type="button"
          aria-pressed={rating === value}
          aria-label={label}
          title={label}
          aria-busy={mutation.isPending}
          onClick={() => toggle(value)}
          className={`rounded-full p-1.5 text-sm leading-none transition ${
            rating === value
              ? compactActive[value]
              : 'text-white/70 hover:bg-white/15 hover:text-white'
          }`}
        >
          {symbol}
        </button>
      ))}
    </div>
  )
}
