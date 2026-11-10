import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { addWatched, removeWatched } from '../../api/watched'
import { applyOptimisticWatched } from './watchedCache'

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
    mutationFn: async (next: boolean) => {
      if (next) await addWatched(titleId)
      else await removeWatched(titleId)
    },
    onMutate: async (next) => {
      await queryClient.cancelQueries({ queryKey: ['recommendations'] })
      await queryClient.cancelQueries({ queryKey: ['title', titleId] })
      const previousReco = queryClient.getQueriesData({ queryKey: ['recommendations'] })
      const previousDetail = queryClient.getQueryData(['title', titleId])
      applyOptimisticWatched(queryClient, titleId, next)
      return { previousReco, previousDetail }
    },
    onError: (_err, _next, ctx) => {
      if (!ctx) return
      for (const [key, data] of ctx.previousReco) {
        queryClient.setQueryData(key, data)
      }
      queryClient.setQueryData(['title', titleId], ctx.previousDetail)
    },
    onSettled: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['titles'] }),
        queryClient.invalidateQueries({ queryKey: ['title', titleId] }),
        queryClient.invalidateQueries({ queryKey: ['recommendations'] }),
        queryClient.invalidateQueries({ queryKey: ['reco-status'] }),
      ])
    },
  })

  const toggle = () => mutation.mutate(!isWatched)

  if (variant === 'dialog') {
    return (
      <button
        type="button"
        aria-pressed={isWatched}
        aria-label={isWatched ? t('watched.remove') : t('watched.add')}
        disabled={mutation.isPending}
        onClick={toggle}
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
        toggle()
      }}
      className={`rounded-full p-1.5 text-sm leading-none transition hover:bg-white/10 ${
        isWatched ? 'text-sky-400' : 'text-white/50'
      } ${className}`}
    >
      ✓
    </button>
  )
}
