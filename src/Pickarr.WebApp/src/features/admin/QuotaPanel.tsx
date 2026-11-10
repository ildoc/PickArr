import { useQuery } from '@tanstack/react-query'
import { useTranslation } from 'react-i18next'
import { fetchAdminQuota } from '../../api/admin'
import { formatDate } from '../../lib/format'

export function QuotaPanel() {
  const { t, i18n } = useTranslation()
  const quota = useQuery({
    queryKey: ['admin', 'quota'],
    queryFn: fetchAdminQuota,
    refetchInterval: 30_000,
  })

  if (quota.isLoading) {
    return <p className="text-sm text-[var(--muted)]">{t('admin.loading')}</p>
  }

  if (quota.isError || !quota.data) {
    return <p className="text-sm text-red-300">{t('admin.quotaError')}</p>
  }

  const data = quota.data
  const usedPct = data.quota > 0 ? Math.min(100, Math.round((data.used / data.quota) * 100)) : 0

  return (
    <section className="space-y-4">
      <h2 className="text-lg font-semibold">{t('admin.quotaHeading')}</h2>
      <p className="text-sm text-[var(--muted)]">{t('admin.quotaService', { service: data.service })}</p>

      <div className="h-2 overflow-hidden rounded-full bg-white/10">
        <div className="h-full bg-emerald-500 transition-all" style={{ width: `${usedPct}%` }} />
      </div>

      <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-3 text-sm">
        <Stat label={t('admin.quotaUsed')} value={`${data.used} / ${data.quota}`} />
        <Stat label={t('admin.quotaReserve')} value={String(data.reserve)} />
        <Stat label={t('admin.quotaSpendable')} value={String(data.spendable)} />
        <Stat
          label={t('admin.quotaProjected')}
          value={data.projectedMonthEnd != null ? String(data.projectedMonthEnd) : '—'}
        />
        <Stat
          label={t('admin.quotaPeriod')}
          value={`${formatDate(data.periodStart, i18n.language)} → ${formatDate(data.periodEnd, i18n.language)}`}
        />
        <Stat
          label={t('admin.quotaUpdated')}
          value={formatDate(data.lastUpdatedAt, i18n.language)}
        />
      </dl>
    </section>
  )
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border border-white/10 bg-[var(--surface-2)] px-3 py-2">
      <dt className="text-xs uppercase tracking-wide text-[var(--muted)]">{label}</dt>
      <dd className="mt-1 text-base font-medium">{value}</dd>
    </div>
  )
}
