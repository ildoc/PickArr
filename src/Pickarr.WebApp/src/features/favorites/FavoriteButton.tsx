import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { addFavorite, removeFavorite } from '../../api/favorites'
import { applyOptimisticFavorite } from './favoriteCache'

export function FavoriteButton({
  titleId,
  isFavorite,
  className = '',
}: {
  titleId: number
  isFavorite: boolean
  className?: string
}) {
  const { t } = useTranslation()
  const queryClient = useQueryClient()

  const mutation = useMutation({
    mutationFn: async (next: boolean) => {
      if (next) await addFavorite(titleId)
      else await removeFavorite(titleId)
    },
    onMutate: async (next) => {
      await Promise.all([
        queryClient.cancelQueries({ queryKey: ['titles'] }),
        queryClient.cancelQueries({ queryKey: ['my-list'] }),
        queryClient.cancelQueries({ queryKey: ['title', titleId] }),
        queryClient.cancelQueries({ queryKey: ['recommendations'] }),
      ])
      const previousTitles = queryClient.getQueriesData({ queryKey: ['titles'] })
      const previousMyList = queryClient.getQueriesData({ queryKey: ['my-list'] })
      const previousTitle = queryClient.getQueriesData({ queryKey: ['title', titleId] })
      const previousReco = queryClient.getQueriesData({ queryKey: ['recommendations'] })
      applyOptimisticFavorite(queryClient, titleId, next)
      return { previousTitles, previousMyList, previousTitle, previousReco }
    },
    onError: (_err, _next, context) => {
      if (!context) return
      for (const [key, data] of context.previousTitles) queryClient.setQueryData(key, data)
      for (const [key, data] of context.previousMyList) queryClient.setQueryData(key, data)
      for (const [key, data] of context.previousTitle) queryClient.setQueryData(key, data)
      for (const [key, data] of context.previousReco) queryClient.setQueryData(key, data)
    },
    onSettled: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ['titles'] }),
        queryClient.invalidateQueries({ queryKey: ['my-list'] }),
        queryClient.invalidateQueries({ queryKey: ['title', titleId] }),
        queryClient.invalidateQueries({ queryKey: ['recommendations'] }),
      ])
    },
  })

  return (
    <button
      type="button"
      aria-pressed={isFavorite}
      aria-label={isFavorite ? t('favorite.remove') : t('favorite.add')}
      title={isFavorite ? t('favorite.remove') : t('favorite.add')}
      aria-busy={mutation.isPending}
      onClick={(e) => {
        e.preventDefault()
        e.stopPropagation()
        mutation.mutate(!isFavorite)
      }}
      className={`rounded-full p-1.5 text-lg leading-none transition hover:bg-white/10 ${isFavorite ? 'text-rose-400' : 'text-white/50'} ${className}`}
    >
      {isFavorite ? '♥' : '♡'}
    </button>
  )
}
