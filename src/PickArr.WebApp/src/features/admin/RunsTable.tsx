import { useQuery } from '@tanstack/react-query'
import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { fetchAdminRuns, type AdminJob } from '../../api/admin'
import { formatDate } from '../../lib/format'

const RUN_STATUSES = ['', 'Running', 'Failed', 'Paused', 'Succeeded', 'NeedsReview'] as const
const PAGE_SIZES = [20, 50, 100] as const

export function RunsTable({
  jobs,
  jobKey,
  onJobKeyChange,
}: {
  jobs: AdminJob[]
  jobKey: string
  onJobKeyChange: (jobKey: string) => void
}) {
  const { t, i18n } = useTranslation()
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState<(typeof PAGE_SIZES)[number]>(20)

  useEffect(() => {
    setPage(1)
  }, [status, jobKey, pageSize])

  const runs = useQuery({
    queryKey: ['admin', 'runs', status, jobKey, page, pageSize],
    queryFn: () =>
      fetchAdminRuns({
        page,
        pageSize,
        status: status || undefined,
        jobKey: jobKey || undefined,
      }),
    refetchInterval: 4_000,
  })

  const total = runs.data?.total ?? 0
  const pageCount = Math.max(1, Math.ceil(total / pageSize))

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="text-lg font-semibold">{t('admin.runsHeading')}</h2>
        <div className="flex flex-wrap items-center gap-3">
        <label className="flex items-center gap-2 text-sm text-[var(--muted)]">
          {t('admin.colJob')}
          <select
            className="rounded-md border border-white/10 bg-[var(--surface-2)] px-2 py-1 text-[var(--text)]"
            value={jobKey}
            onChange={(e) => onJobKeyChange(e.target.value)}
          >
            <option value="">{t('admin.runsJobAll')}</option>
            {jobs.map((job) => (
              <option key={job.jobKey} value={job.jobKey}>
                {job.displayName}
              </option>
            ))}
          </select>
        </label>
        <label className="flex items-center gap-2 text-sm text-[var(--muted)]">
          {t('admin.colStatus')}
          <select
            className="rounded-md border border-white/10 bg-[var(--surface-2)] px-2 py-1 text-[var(--text)]"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            {RUN_STATUSES.map((value) => (
              <option key={value || 'all'} value={value}>
                {value ? t(`admin.state.${value}`, { defaultValue: value }) : t('admin.runsStatusAll')}
              </option>
            ))}
          </select>
        </label>
        <label className="flex items-center gap-2 text-sm text-[var(--muted)]">
          {t('admin.pageSize')}
          <select
            className="rounded-md border border-white/10 bg-[var(--surface-2)] px-2 py-1 text-[var(--text)]"
            value={pageSize}
            onChange={(e) => setPageSize(Number(e.target.value) as (typeof PAGE_SIZES)[number])}
          >
            {PAGE_SIZES.map((size) => (
              <option key={size} value={size}>
                {size}
              </option>
            ))}
          </select>
        </label>
        </div>
      </div>
      {runs.isLoading && <p className="text-sm text-[var(--muted)]">{t('admin.loading')}</p>}
      {runs.isError && <p className="text-sm text-red-300">{t('admin.runsError')}</p>}
      {runs.data && runs.data.items.length === 0 && (
        <p className="text-sm text-[var(--muted)]">
          {jobKey ? t('admin.runsEmptyJob') : t('admin.runsEmpty')}
        </p>
      )}
      {runs.data && runs.data.items.length > 0 && (
        <div className="overflow-x-auto rounded-lg border border-white/10">
          <table className="min-w-full text-left text-sm">
            <thead className="bg-[var(--surface-2)] text-xs uppercase tracking-wide text-[var(--muted)]">
              <tr>
                <th className="px-3 py-2 font-medium">{t('admin.colKind')}</th>
                <th className="px-3 py-2 font-medium">{t('admin.colSource')}</th>
                <th className="px-3 py-2 font-medium">{t('admin.colStatus')}</th>
                <th className="px-3 py-2 font-medium">{t('admin.colStarted')}</th>
                <th className="px-3 py-2 font-medium">{t('admin.colStats')}</th>
              </tr>
            </thead>
            <tbody>
              {runs.data.items.map((run) => (
                <tr key={run.id} className="border-t border-white/5">
                  <td className="px-3 py-2">{t(`admin.kind.${run.kind}`, { defaultValue: run.kind })}</td>
                  <td className="px-3 py-2">{run.countryCode ?? '—'}</td>
                  <td className="px-3 py-2">
                    <span className="rounded bg-white/10 px-1.5 py-0.5 text-xs">
                      {t(`admin.state.${run.status}`, { defaultValue: run.status })}
                    </span>
                    {run.errorMessage && (
                      <details open className="mt-1 max-w-md">
                        <summary className="cursor-pointer text-xs text-red-300">{t('admin.failureLog')}</summary>
                        <pre className="mt-1 whitespace-pre-wrap break-words text-xs text-red-200">
                          {run.errorMessage}
                        </pre>
                      </details>
                    )}
                  </td>
                  <td className="px-3 py-2 text-[var(--muted)]">
                    {formatDate(run.startedAt, i18n.language)}
                    <div className="text-xs">{run.triggeredBy}</div>
                  </td>
                  <td className="px-3 py-2 text-xs text-[var(--muted)]">
                    {t('admin.runStats', {
                      credits: run.creditsUsed,
                      pages: run.pagesFetched,
                      added: run.titlesAdded,
                      updated: run.titlesUpdated,
                    })}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {runs.data && total > 0 && (
        <div className="flex items-center justify-end gap-3 text-sm">
          <button
            type="button"
            className="rounded-md bg-white/10 px-2.5 py-1 disabled:opacity-40"
            disabled={page <= 1}
            onClick={() => setPage((current) => Math.max(1, current - 1))}
          >
            {t('admin.prev')}
          </button>
          <span className="text-[var(--muted)]">{t('admin.pageOf', { page, pages: pageCount })}</span>
          <button
            type="button"
            className="rounded-md bg-white/10 px-2.5 py-1 disabled:opacity-40"
            disabled={page >= pageCount}
            onClick={() => setPage((current) => current + 1)}
          >
            {t('admin.next')}
          </button>
        </div>
      )}
    </section>
  )
}
