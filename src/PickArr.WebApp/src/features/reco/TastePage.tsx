import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate } from 'react-router-dom'
import { fetchOnboarding, postOnboardingRatings } from '../../api/recommendations'
import { fetchSettings } from '../../api/settings'
import { loadPreferredSourceId } from '../../lib/sourcePreference'

export function TastePage() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const settings = useQuery({ queryKey: ['settings'], queryFn: fetchSettings })
  const sourceId = loadPreferredSourceId()
  const [votes, setVotes] = useState<Record<number, 'like' | 'dislike' | null>>({})

  const onboarding = useQuery({
    queryKey: ['onboarding', sourceId],
    queryFn: () => fetchOnboarding(sourceId),
    enabled: settings.data?.reco.enabled !== false,
  })

  const submit = useMutation({
    mutationFn: () =>
      postOnboardingRatings(
        Object.entries(votes).map(([titleId, value]) => ({
          titleId: Number(titleId),
          value,
        })),
      ),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['reco'] })
      await queryClient.invalidateQueries({ queryKey: ['recommendations'] })
      navigate('/for-you')
    },
  })

  if (settings.data && !settings.data.reco.enabled) {
    return <p className="text-[var(--muted)]">{t('reco.disabled')}</p>
  }

  return (
    <div className="mx-auto max-w-5xl space-y-6">
      <div>
        <h1 className="text-3xl font-semibold tracking-tight">{t('reco.tasteHeading')}</h1>
        <p className="mt-2 text-[var(--muted)]">{t('reco.tasteIntro')}</p>
      </div>

      {onboarding.isLoading && <p className="text-[var(--muted)]">{t('catalog.loading')}</p>}
      {onboarding.isError && <p>{t('reco.loadError')}</p>}

      {onboarding.data && (
        <ul className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4">
          {onboarding.data.items.map((item) => {
            const vote = votes[item.id] ?? null
            return (
              <li key={item.id} className="space-y-2">
                {item.posterUrl ? (
                  <img src={item.posterUrl} alt="" className="aspect-[2/3] w-full rounded-lg object-cover" />
                ) : (
                  <div className="aspect-[2/3] w-full rounded-lg bg-white/5" />
                )}
                <p className="line-clamp-2 text-sm font-medium">{item.name}</p>
                <div className="flex gap-2">
                  <button
                    type="button"
                    className={`rounded px-2 py-1 text-xs ${vote === 'like' ? 'bg-emerald-600 text-white' : 'bg-white/10'}`}
                    onClick={() =>
                      setVotes((v) => ({ ...v, [item.id]: vote === 'like' ? null : 'like' }))
                    }
                  >
                    {t('ratings.like')}
                  </button>
                  <button
                    type="button"
                    className={`rounded px-2 py-1 text-xs ${vote === 'dislike' ? 'bg-rose-600 text-white' : 'bg-white/10'}`}
                    onClick={() =>
                      setVotes((v) => ({ ...v, [item.id]: vote === 'dislike' ? null : 'dislike' }))
                    }
                  >
                    {t('ratings.dislike')}
                  </button>
                </div>
              </li>
            )
          })}
        </ul>
      )}

      <div className="flex flex-wrap items-center gap-3">
        <button
          type="button"
          disabled={submit.isPending || Object.keys(votes).length === 0}
          onClick={() => submit.mutate()}
          className="rounded-md bg-[var(--accent)] px-4 py-2 text-sm font-medium text-white disabled:opacity-50"
        >
          {t('reco.tasteSubmit')}
        </button>
        <Link to="/for-you" className="text-sm text-[var(--muted)] underline">
          {t('reco.tasteSkip')}
        </Link>
      </div>
    </div>
  )
}
