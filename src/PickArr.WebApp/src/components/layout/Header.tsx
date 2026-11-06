import type { ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { NavLink } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { fetchSettings } from '../../api/settings'

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `text-sm ${isActive ? 'text-[var(--text)]' : 'text-[var(--muted)] hover:text-[var(--text)]'}`

export function Header({ children }: { children?: ReactNode }) {
  const { t } = useTranslation()
  const settings = useQuery({ queryKey: ['settings'], queryFn: fetchSettings })
  const recoEnabled = settings.data?.reco.enabled !== false

  return (
    <header className="flex items-center gap-8 border-b border-white/10 px-8 py-4">
      <NavLink to="/catalog" className="text-xl font-semibold tracking-tight text-[var(--text)]">
        {t('app.title')}
      </NavLink>
      <nav className="flex items-center gap-5">
        <NavLink to="/catalog" end className={linkClass}>
          {t('nav.catalog')}
        </NavLink>
        {recoEnabled && (
          <NavLink to="/for-you" className={linkClass}>
            {t('nav.forYou')}
          </NavLink>
        )}
        <NavLink to="/list" className={linkClass}>
          {t('nav.myList')}
        </NavLink>
        {recoEnabled && (
          <NavLink to="/ratings" className={linkClass}>
            {t('nav.ratings')}
          </NavLink>
        )}
        <NavLink to="/settings" className={linkClass}>
          {t('nav.settings')}
        </NavLink>
      </nav>
      <div className="ml-auto flex items-center gap-4">{children}</div>
    </header>
  )
}
