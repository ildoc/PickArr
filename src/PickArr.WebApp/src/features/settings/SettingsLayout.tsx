import { NavLink, Outlet } from 'react-router-dom'
import { useTranslation } from 'react-i18next'

const itemClass = ({ isActive }: { isActive: boolean }) =>
  `inline-flex w-40 justify-center rounded-full px-4 py-1.5 text-center text-sm ${
    isActive
      ? 'bg-[var(--accent)] text-white'
      : 'bg-[var(--surface-2)] text-[var(--muted)] hover:text-[var(--text)]'
  }`

export function SettingsLayout() {
  const { t } = useTranslation()

  return (
    <div className="mx-auto min-h-[60vh] w-full max-w-7xl space-y-6 rounded-xl border border-white/10 bg-[var(--surface)] p-6">
      <div className="space-y-4">
        <h1 className="text-3xl font-semibold tracking-tight">{t('settings.heading')}</h1>
        <nav aria-label={t('settings.sections')} className="flex flex-wrap gap-2">
          <NavLink to="/settings/system" className={itemClass}>
            {t('settings.system')}
          </NavLink>
          <NavLink to="/settings/users" className={itemClass}>
            {t('settings.users')}
          </NavLink>
          <NavLink to="/settings/catalogs" className={itemClass}>
            {t('settings.catalogs')}
          </NavLink>
          <NavLink to="/settings/recommendations" className={itemClass}>
            {t('settings.recommendations')}
          </NavLink>
          <NavLink to="/settings/connections" className={itemClass}>
            {t('settings.connections')}
          </NavLink>
          <NavLink to="/settings/jobs" className={itemClass}>
            {t('settings.jobs')}
          </NavLink>
        </nav>
      </div>
      <Outlet />
    </div>
  )
}
