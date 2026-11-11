import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import {
  clearSystemLogs,
  downloadSystemLog,
  fetchSystemLogs,
  fetchSystemStats,
  fetchSystemStatus,
  type StorageEntry,
} from '../../api/system'
import { formatBytes, formatDateTime } from '../../lib/format'

const regionNames = new Intl.DisplayNames(['en'], { type: 'region' })

function countryLabel(code: string): string {
  return regionNames.of(code) ?? code
}

function storageLabelKey(entry: StorageEntry): string {
  return `settings.storage.${entry.key}`
}

export function SystemPage() {
  const { t, i18n } = useTranslation()
  const queryClient = useQueryClient()
  const [logMessage, setLogMessage] = useState<string | null>(null)
  const status = useQuery({ queryKey: ['system', 'status'], queryFn: fetchSystemStatus })
  const stats = useQuery({ queryKey: ['system', 'stats'], queryFn: fetchSystemStats })
  const logs = useQuery({ queryKey: ['system', 'logs'], queryFn: fetchSystemLogs })

  const clearLogs = useMutation({
    mutationFn: clearSystemLogs,
    onSuccess: async (result) => {
      setLogMessage(
        result.failed > 0
          ? t('settings.systemLogsClearPartial', result)
          : t('settings.systemLogsCleared', result),
      )
      await queryClient.invalidateQueries({ queryKey: ['system', 'logs'] })
      await queryClient.invalidateQueries({ queryKey: ['system', 'stats'] })
    },
  })

  if (stats.isLoading) {
    return <p className="text-sm text-[var(--muted)]">{t('settings.systemLoading')}</p>
  }

  if (stats.isError || !stats.data) {
    return <p className="text-sm text-red-300">{t('settings.systemError')}</p>
  }

  const data = stats.data

  return (
    <div className="mx-auto max-w-3xl space-y-8">
      <p className="text-sm text-[var(--muted)]">{t('settings.systemIntro')}</p>

      {(status.data?.version || status.data?.utcNow) && (
        <section className="space-y-3 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
          <h2 className="text-lg font-medium">{t('settings.systemAbout')}</h2>
          <dl className="grid gap-3 sm:grid-cols-2 text-sm">
            {status.data.version && (
              <div>
                <dt className="text-xs uppercase tracking-wide text-[var(--muted)]">
                  {t('settings.systemVersion')}
                </dt>
                <dd className="mt-1 font-medium">{status.data.version}</dd>
              </div>
            )}
            {status.data.utcNow && (
              <div>
                <dt className="text-xs uppercase tracking-wide text-[var(--muted)]">
                  {t('settings.systemServerTime')}
                </dt>
                <dd className="mt-1 font-medium">
                  {new Date(status.data.utcNow).toLocaleString(i18n.language)}
                </dd>
              </div>
            )}
          </dl>
        </section>
      )}

      <section className="space-y-4 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 className="text-lg font-medium">{t('settings.systemTitles')}</h2>
          <p className="text-2xl font-semibold tracking-tight">
            {t('settings.systemTotalTitles', { count: data.totalTitles })}
          </p>
        </div>
        <p className="text-xs text-[var(--muted)]">{t('settings.systemTitlesHint')}</p>

        <div className="grid gap-6 lg:grid-cols-2">
          <CountTable
            heading={t('settings.systemByProvider')}
            rows={data.byProvider.map((row) => ({
              key: row.name,
              label: row.name,
              count: row.titleCount,
            }))}
            empty={t('settings.systemNoBreakdown')}
          />
          <CountTable
            heading={t('settings.systemByCountry')}
            rows={data.byCountry.map((row) => ({
              key: row.name,
              label: countryLabel(row.name),
              count: row.titleCount,
            }))}
            empty={t('settings.systemNoBreakdown')}
          />
        </div>

        <div className="space-y-2">
          <h3 className="text-sm font-medium text-[var(--muted)]">{t('settings.systemBySource')}</h3>
          {data.bySource.length === 0 ? (
            <p className="text-sm text-[var(--muted)]">{t('settings.systemNoBreakdown')}</p>
          ) : (
            <div className="overflow-x-auto rounded-lg border border-white/10">
              <table className="min-w-full text-left text-sm">
                <thead className="bg-black/20 text-xs uppercase tracking-wide text-[var(--muted)]">
                  <tr>
                    <th className="px-3 py-2 font-medium">{t('catalog.provider')}</th>
                    <th className="px-3 py-2 font-medium">{t('catalog.country')}</th>
                    <th className="px-3 py-2 font-medium text-right">{t('settings.systemColTitles')}</th>
                  </tr>
                </thead>
                <tbody>
                  {data.bySource.map((row) => (
                    <tr
                      key={`${row.providerName}-${row.countryCode}`}
                      className="border-t border-white/5"
                    >
                      <td className="px-3 py-2">{row.providerName}</td>
                      <td className="px-3 py-2">{countryLabel(row.countryCode)}</td>
                      <td className="px-3 py-2 text-right tabular-nums">
                        {row.titleCount.toLocaleString(i18n.language)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </section>

      <section className="space-y-4 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
        <div className="flex flex-wrap items-baseline justify-between gap-2">
          <h2 className="text-lg font-medium">{t('settings.systemStorage')}</h2>
          <p className="text-base font-medium">
            {t('settings.systemStorageTotal', {
              size: formatBytes(data.storage.totalBytes, i18n.language),
            })}
          </p>
        </div>
        <p className="text-xs text-[var(--muted)]">{t('settings.systemStorageHint')}</p>
        <div className="overflow-x-auto rounded-lg border border-white/10">
          <table className="min-w-full text-left text-sm">
            <thead className="bg-black/20 text-xs uppercase tracking-wide text-[var(--muted)]">
              <tr>
                <th className="px-3 py-2 font-medium">{t('settings.systemColItem')}</th>
                <th className="px-3 py-2 font-medium">{t('settings.systemColPath')}</th>
                <th className="px-3 py-2 font-medium text-right">{t('settings.systemColSize')}</th>
              </tr>
            </thead>
            <tbody>
              {data.storage.entries.map((entry) => (
                <tr key={entry.key} className="border-t border-white/5">
                  <td className="px-3 py-2">
                    {t(storageLabelKey(entry), { defaultValue: entry.key })}
                  </td>
                  <td className="px-3 py-2 text-[var(--muted)]">
                    {entry.relativePath ?? '—'}
                  </td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {formatBytes(entry.bytes, i18n.language)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="space-y-4 rounded-xl border border-white/10 bg-[var(--surface-2)] p-5">
        <div className="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h2 className="text-lg font-medium">{t('settings.systemLogs')}</h2>
            <p className="mt-1 text-xs text-[var(--muted)]">{t('settings.systemLogsHint')}</p>
          </div>
          <button
            type="button"
            disabled={clearLogs.isPending || (logs.data?.items.length ?? 0) === 0}
            onClick={() => {
              if (window.confirm(t('settings.systemLogsClearConfirm'))) {
                clearLogs.mutate()
              }
            }}
            className="rounded-md bg-white/10 px-3 py-1.5 text-sm hover:bg-white/15 disabled:opacity-50"
          >
            {t('settings.systemLogsClear')}
          </button>
        </div>
        {logMessage && <p className="text-sm text-emerald-300">{logMessage}</p>}
        {logs.isLoading && <p className="text-sm text-[var(--muted)]">{t('settings.systemLoading')}</p>}
        {logs.isError && <p className="text-sm text-red-300">{t('settings.systemLogsError')}</p>}
        {logs.data && logs.data.items.length === 0 && (
          <p className="text-sm text-[var(--muted)]">{t('settings.systemLogsEmpty')}</p>
        )}
        {logs.data && logs.data.items.length > 0 && (
          <div className="overflow-x-auto rounded-lg border border-white/10">
            <table className="min-w-full text-left text-sm">
              <thead className="bg-black/20 text-xs uppercase tracking-wide text-[var(--muted)]">
                <tr>
                  <th className="px-3 py-2 font-medium">{t('settings.systemLogsColName')}</th>
                  <th className="px-3 py-2 font-medium">{t('settings.systemLogsColModified')}</th>
                  <th className="px-3 py-2 font-medium text-right">{t('settings.systemColSize')}</th>
                  <th className="px-3 py-2 font-medium" />
                </tr>
              </thead>
              <tbody>
                {logs.data.items.map((file) => (
                  <tr key={file.fileName} className="border-t border-white/5">
                    <td className="px-3 py-2 font-mono text-xs">{file.fileName}</td>
                    <td className="px-3 py-2 text-[var(--muted)]">
                      {formatDateTime(file.lastWriteUtc, i18n.language)}
                    </td>
                    <td className="px-3 py-2 text-right tabular-nums">
                      {formatBytes(file.bytes, i18n.language)}
                    </td>
                    <td className="px-3 py-2 text-right">
                      <button
                        type="button"
                        className="rounded-md bg-white/10 px-2.5 py-1 text-xs hover:bg-white/15"
                        onClick={() => void downloadSystemLog(file.fileName)}
                      >
                        {t('settings.systemLogsDownload')}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>
    </div>
  )
}

function CountTable({
  heading,
  rows,
  empty,
}: {
  heading: string
  rows: Array<{ key: string; label: string; count: number }>
  empty: string
}) {
  const { i18n } = useTranslation()
  return (
    <div className="space-y-2">
      <h3 className="text-sm font-medium text-[var(--muted)]">{heading}</h3>
      {rows.length === 0 ? (
        <p className="text-sm text-[var(--muted)]">{empty}</p>
      ) : (
        <div className="overflow-x-auto rounded-lg border border-white/10">
          <table className="min-w-full text-left text-sm">
            <tbody>
              {rows.map((row) => (
                <tr key={row.key} className="border-t border-white/5 first:border-t-0">
                  <td className="px-3 py-2">{row.label}</td>
                  <td className="px-3 py-2 text-right tabular-nums">
                    {row.count.toLocaleString(i18n.language)}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
