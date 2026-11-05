import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { ApiError } from '../../api/client'
import { fetchAdminJobs, triggerAdminJob, type AdminJob } from '../../api/admin'
import { formatDate } from '../../lib/format'
import { QuotaPanel } from './QuotaPanel'
import { RunsTable } from './RunsTable'

const STATE_RANK: Record<string, number> = {
  Running: 0,
  Queued: 1,
  Failed: 2,
  NeedsReview: 3,
  Paused: 4,
  PartiallySucceeded: 5,
  Succeeded: 6,
  Scheduled: 7,
  Idle: 8,
}

function stateClass(state: string): string {
  switch (state) {
    case 'Running':
      return 'bg-sky-500/20 text-sky-200'
    case 'Queued':
      return 'bg-amber-500/20 text-amber-200'
    case 'Failed':
    case 'NeedsReview':
      return 'bg-red-500/20 text-red-200'
    case 'Paused':
      return 'bg-amber-500/15 text-amber-100'
    case 'Succeeded':
    case 'PartiallySucceeded':
      return 'bg-emerald-500/20 text-emerald-200'
    default:
      return 'bg-white/10 text-[var(--muted)]'
  }
}

export function JobsPage() {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const [message, setMessage] = useState<string | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [jobKey, setJobKey] = useState<string | undefined>(undefined)

  const jobs = useQuery({
    queryKey: ['admin', 'jobs'],
    queryFn: fetchAdminJobs,
    refetchInterval: 4_000,
  })

  const trigger = useMutation({
    mutationFn: (job: AdminJob) => {
      if (job.jobKey.startsWith('catalog-incr-') || job.jobKey.startsWith('catalog-full-')) {
        return triggerAdminJob(job.jobKey, {
          sourceId: job.sourceId ?? undefined,
          mode: job.jobKey.includes('-full-') ? 'full' : 'incremental',
        })
      }
      return triggerAdminJob(job.jobKey)
    },
    onSuccess: async (result) => {
      setError(null)
      setMessage(
        result.runId
          ? t('admin.triggerOkRun', { runId: result.runId })
          : t('admin.triggerOk', { jobId: result.hangfireJobId }),
      )
      await queryClient.invalidateQueries({ queryKey: ['admin'] })
    },
    onError: (err: unknown) => {
      setMessage(null)
      if (err instanceof ApiError) {
        setError(err.message)
        return
      }
      setError(t('admin.triggerFailed'))
    },
  })

  const runningKey = jobs.data?.find((job) => job.executionState === 'Running')?.jobKey
  const selectedJobKey = jobKey ?? runningKey ?? ''

  return (
    <div className="space-y-10">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{t('admin.heading')}</h1>
        <p className="mt-1 text-sm text-[var(--muted)]">{t('admin.subtitle')}</p>
      </div>

      <QuotaPanel />

      <section className="space-y-3">
        <h2 className="text-lg font-semibold">{t('admin.jobsHeading')}</h2>
        {message && <p className="text-sm text-emerald-300">{message}</p>}
        {error && <p className="text-sm text-red-300">{error}</p>}
        {jobs.isLoading && <p className="text-sm text-[var(--muted)]">{t('admin.loading')}</p>}
        {jobs.isError && <p className="text-sm text-red-300">{t('admin.jobsError')}</p>}
        {jobs.data && (
          <>
            <p className="text-sm text-[var(--muted)]">
              {t('admin.jobSummary', {
                running: jobs.data.filter((job) => job.executionState === 'Running').length,
                queued: jobs.data.filter((job) => job.executionState === 'Queued').length,
                failed: jobs.data.filter(
                  (job) => job.executionState === 'Failed' || job.executionState === 'NeedsReview',
                ).length,
              })}
            </p>
            <div className="overflow-x-auto rounded-lg border border-white/10">
              <table className="min-w-full text-left text-sm">
                <thead className="bg-[var(--surface-2)] text-xs uppercase tracking-wide text-[var(--muted)]">
                  <tr>
                    <th className="px-3 py-2 font-medium">{t('admin.colJob')}</th>
                    <th className="px-3 py-2 font-medium">{t('admin.colStatus')}</th>
                    <th className="px-3 py-2 font-medium">{t('admin.colLast')}</th>
                    <th className="px-3 py-2 font-medium">{t('admin.colNext')}</th>
                    <th className="px-3 py-2 font-medium" />
                  </tr>
                </thead>
                <tbody>
                  {[...jobs.data]
                    .sort(
                      (a, b) =>
                        (STATE_RANK[a.executionState] ?? 9) - (STATE_RANK[b.executionState] ?? 9),
                    )
                    .map((job) => (
                      <tr key={job.jobKey} className="border-t border-white/5 align-top">
                        <td className="px-3 py-2">
                          <button
                            type="button"
                            onClick={() => setJobKey(job.jobKey)}
                            className={`text-left font-medium hover:underline ${
                              selectedJobKey === job.jobKey ? 'text-sky-200' : ''
                            }`}
                          >
                            {job.displayName}
                          </button>
                          {job.description && (
                            <p className="mt-1 max-w-md text-xs text-[var(--muted)]">{job.description}</p>
                          )}
                          <div className="mt-1 text-xs text-[var(--muted)]">
                            {job.jobKey}
                            {job.cron ? ` · ${job.cron}` : ''}
                          </div>
                          {job.lastError && (
                            <details open className="mt-2 max-w-md">
                              <summary className="cursor-pointer text-xs text-red-300">
                                {t('admin.failureLog')}
                              </summary>
                              <pre className="mt-1 whitespace-pre-wrap break-words text-xs text-red-200">
                                {job.lastError}
                              </pre>
                            </details>
                          )}
                        </td>
                        <td className="px-3 py-2">
                          <span className={`rounded px-1.5 py-0.5 text-xs ${stateClass(job.executionState)}`}>
                            {t(`admin.state.${job.executionState}`, { defaultValue: job.executionState })}
                          </span>
                        </td>
                        <td className="px-3 py-2 text-[var(--muted)]">
                          {formatDate(job.lastExecution, i18n.language)}
                        </td>
                        <td className="px-3 py-2 text-[var(--muted)]">
                          {formatDate(job.nextExecution, i18n.language)}
                        </td>
                        <td className="px-3 py-2 text-right">
                          <button
                            type="button"
                            disabled={!job.canTrigger || trigger.isPending}
                            onClick={() => trigger.mutate(job)}
                            className="rounded-md bg-white/10 px-2.5 py-1 text-xs hover:bg-white/15 disabled:opacity-50"
                          >
                            {t('admin.runNow')}
                          </button>
                        </td>
                      </tr>
                    ))}
                </tbody>
              </table>
            </div>
          </>
        )}
      </section>

      <RunsTable jobs={jobs.data ?? []} jobKey={selectedJobKey} onJobKeyChange={setJobKey} />
    </div>
  )
}
