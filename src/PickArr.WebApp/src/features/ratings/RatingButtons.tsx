import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import type { RatingValue } from '../../api/ratings'
import { fetchSettings } from '../../api/settings'
import type { UserRating } from './ratingCache'
import { useRatingMutation } from './useRatingMutation'

export function RatingButtons({
  titleId,
  rating,
  className = '',
}: {
  titleId: number
  rating: UserRating
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

  return (
    <div
      className={`flex items-center gap-0.5 ${className}`}
      onClick={(e) => {
        e.preventDefault()
        e.stopPropagation()
      }}
    >
      <button
        type="button"
        aria-pressed={rating === 'like'}
        aria-label={t('ratings.like')}
        title={t('ratings.like')}
        disabled={mutation.isPending}
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
        disabled={mutation.isPending}
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
