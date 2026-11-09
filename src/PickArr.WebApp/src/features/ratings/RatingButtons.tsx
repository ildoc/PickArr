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
        <button
          type="button"
          aria-pressed={rating === 'love'}
          aria-busy={mutation.isPending}
          onClick={() => toggle('love')}
          className={`rounded-md px-3 py-1.5 text-sm font-medium transition ${
            rating === 'love'
              ? 'bg-amber-500 text-white hover:bg-amber-400'
              : 'bg-white/10 text-[var(--text)] hover:bg-white/15'
          }`}
        >
          {t('ratings.love')}
        </button>
        <button
          type="button"
          aria-pressed={rating === 'like'}
          aria-busy={mutation.isPending}
          onClick={() => toggle('like')}
          className={`rounded-md px-3 py-1.5 text-sm font-medium transition ${
            rating === 'like'
              ? 'bg-emerald-600 text-white hover:bg-emerald-500'
              : 'bg-white/10 text-[var(--text)] hover:bg-white/15'
          }`}
        >
          {t('ratings.like')}
        </button>
        <button
          type="button"
          aria-pressed={rating === 'dislike'}
          aria-busy={mutation.isPending}
          onClick={() => toggle('dislike')}
          className={`rounded-md px-3 py-1.5 text-sm font-medium transition ${
            rating === 'dislike'
              ? 'bg-rose-600 text-white hover:bg-rose-500'
              : 'bg-white/10 text-[var(--text)] hover:bg-white/15'
          }`}
        >
          {t('ratings.dislike')}
        </button>
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
      <button
        type="button"
        aria-pressed={rating === 'love'}
        aria-label={t('ratings.love')}
        title={t('ratings.love')}
        aria-busy={mutation.isPending}
        onClick={() => toggle('love')}
        className={`rounded-full p-1.5 text-sm leading-none transition hover:bg-white/10 ${
          rating === 'love' ? 'text-amber-400' : 'text-white/50'
        }`}
      >
        ★
      </button>
      <button
        type="button"
        aria-pressed={rating === 'like'}
        aria-label={t('ratings.like')}
        title={t('ratings.like')}
        aria-busy={mutation.isPending}
        onClick={() => toggle('like')}
        className={`rounded-full p-1.5 text-sm leading-none transition hover:bg-white/10 ${
          rating === 'like' ? 'text-emerald-400' : 'text-white/50'
        }`}
      >
        ▲
      </button>
      <button
        type="button"
        aria-pressed={rating === 'dislike'}
        aria-label={t('ratings.dislike')}
        title={t('ratings.dislike')}
        aria-busy={mutation.isPending}
        onClick={() => toggle('dislike')}
        className={`rounded-full p-1.5 text-sm leading-none transition hover:bg-white/10 ${
          rating === 'dislike' ? 'text-rose-400' : 'text-white/50'
        }`}
      >
        ▼
      </button>
    </div>
  )
}
