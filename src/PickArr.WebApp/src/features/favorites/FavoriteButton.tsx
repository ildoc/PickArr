import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { addFavorite, removeFavorite } from '../../api/favorites'

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
    mutationFn: async () => {
      if (isFavorite) await removeFavorite(titleId)
      else await addFavorite(titleId)
    },
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['titles'] })
      await queryClient.invalidateQueries({ queryKey: ['my-list'] })
      await queryClient.invalidateQueries({ queryKey: ['title', titleId] })
    },
  })

  return (
    <button
      type="button"
      aria-pressed={isFavorite}
      aria-label={isFavorite ? t('favorite.remove') : t('favorite.add')}
      title={isFavorite ? t('favorite.remove') : t('favorite.add')}
      disabled={mutation.isPending}
      onClick={(e) => {
        e.preventDefault()
        e.stopPropagation()
        mutation.mutate()
      }}
      className={`rounded-full p-1.5 text-lg leading-none transition hover:bg-white/10 ${isFavorite ? 'text-rose-400' : 'text-white/50'} ${className}`}
    >
      {isFavorite ? '♥' : '♡'}
    </button>
  )
}
