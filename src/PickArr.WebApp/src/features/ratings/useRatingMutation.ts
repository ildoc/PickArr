import { useMutation, useQueryClient } from '@tanstack/react-query'
import { clearRating, setRating } from '../../api/ratings'
import { applyOptimisticRating, invalidateRatingQueries, type UserRating } from './ratingCache'

export function useRatingMutation(titleId: number) {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: async (next: UserRating) => {
      if (next == null) await clearRating(titleId)
      else await setRating(titleId, next)
    },
    onMutate: async (next) => {
      await queryClient.cancelQueries({ queryKey: ['titles'] })
      await queryClient.cancelQueries({ queryKey: ['my-list'] })
      await queryClient.cancelQueries({ queryKey: ['title', titleId] })
      await queryClient.cancelQueries({ queryKey: ['recommendations'] })
      await queryClient.cancelQueries({ queryKey: ['ratings'] })

      const previousTitles = queryClient.getQueriesData({ queryKey: ['titles'] })
      const previousMyList = queryClient.getQueriesData({ queryKey: ['my-list'] })
      const previousTitle = queryClient.getQueriesData({ queryKey: ['title', titleId] })
      const previousReco = queryClient.getQueriesData({ queryKey: ['recommendations'] })
      const previousRatings = queryClient.getQueriesData({ queryKey: ['ratings'] })

      applyOptimisticRating(queryClient, titleId, next)
      return { previousTitles, previousMyList, previousTitle, previousReco, previousRatings }
    },
    onError: (_err, _next, context) => {
      if (!context) return
      for (const [key, data] of context.previousTitles) queryClient.setQueryData(key, data)
      for (const [key, data] of context.previousMyList) queryClient.setQueryData(key, data)
      for (const [key, data] of context.previousTitle) queryClient.setQueryData(key, data)
      for (const [key, data] of context.previousReco) queryClient.setQueryData(key, data)
      for (const [key, data] of context.previousRatings) queryClient.setQueryData(key, data)
    },
    onSettled: async () => {
      await invalidateRatingQueries(queryClient, titleId)
    },
  })
}
