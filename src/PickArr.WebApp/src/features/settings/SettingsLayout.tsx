import { NavLink, Outlet } from 'react-router-dom'
import { useTranslation } from 'react-i18next'

const itemClass = ({ isActive }: { isActive: boolean }) =>
  `rounded-full px-4 py-1.5 text-sm ${
    isActive ? 'bg-white/10 text-[var(--text)]' : 'text-[var(--muted)] hover:text-[var(--text)]'
  }`

export function SettingsLayout() {
  const { t } = useTranslation()

  return (
    <div className="space-y-6">
      <div className="space-y-4">
        <h1 className="text-3xl font-semibold tracking-tight">{t('settings.heading')}</h1>
        <nav className="flex flex-wrap gap-2">
          <NavLink to="/settings" end className={itemClass}>
            {t('settings.connections')}
          </NavLink>
          <NavLink to="/settings/catalogs" className={itemClass}>
            {t('settings.catalogs')}
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
