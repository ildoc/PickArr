import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { addWatched, removeWatched } from '../../api/watched'

export function WatchedButton({
  titleId,
  isWatched,
  variant = 'compact',
  className = '',
}: {
  titleId: number
  isWatched: boolean
  variant?: 'compact' | 'dialog'
  className?: string
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: async () => {
      if (isWatched) await removeWatched(titleId)
      else await addWatched(titleId)
    },
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['titles'] }),
        queryClient.invalidateQueries({ queryKey: ['title', titleId] }),
        queryClient.invalidateQueries({ queryKey: ['recommendations'] }),
        queryClient.invalidateQueries({ queryKey: ['reco-status'] }),
      ])
    },
  })

  if (variant === 'dialog') {
    return (
      <button
        type="button"
        aria-pressed={isWatched}
        aria-label={isWatched ? t('watched.remove') : t('watched.add')}
        disabled={mutation.isPending}
        onClick={() => mutation.mutate()}
        className={`rounded-md px-3 py-1.5 text-sm font-medium transition ${
          isWatched
            ? 'bg-sky-600 text-white hover:bg-sky-500'
            : 'bg-white/10 text-[var(--text)] hover:bg-white/15'
        } ${className}`}
      >
        {isWatched ? t('watched.marked') : t('watched.add')}
      </button>
    )
  }

  return (
    <button
      type="button"
      aria-pressed={isWatched}
      aria-label={isWatched ? t('watched.remove') : t('watched.add')}
      title={isWatched ? t('watched.remove') : t('watched.add')}
      disabled={mutation.isPending}
      onClick={(e) => {
        e.preventDefault()
        e.stopPropagation()
        mutation.mutate()
      }}
      className={`rounded-full p-1.5 text-sm leading-none transition hover:bg-white/10 ${
        isWatched ? 'text-sky-400' : 'text-white/50'
      } ${className}`}
    >
      ✓
    </button>
  )
}
