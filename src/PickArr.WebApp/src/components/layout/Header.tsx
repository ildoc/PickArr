import type { ReactNode } from 'react'
import { NavLink } from 'react-router-dom'
import { useTranslation } from 'react-i18next'

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `text-sm ${isActive ? 'text-[var(--text)]' : 'text-[var(--muted)] hover:text-[var(--text)]'}`

export function Header({ children }: { children?: ReactNode }) {
  const { t } = useTranslation()

  return (
    <header className="flex items-center gap-8 border-b border-white/10 px-8 py-4">
      <NavLink to="/catalog" className="text-xl font-semibold tracking-tight text-[var(--text)]">
        {t('app.title')}
      </NavLink>
      <nav className="flex items-center gap-5">
        <NavLink to="/catalog" end className={linkClass}>
          {t('nav.catalog')}
        </NavLink>
        <NavLink to="/list" className={linkClass}>
          {t('nav.myList')}
        </NavLink>
        <NavLink to="/settings" className={linkClass}>
          {t('nav.settings')}
        </NavLink>
      </nav>
      <div className="ml-auto flex items-center gap-4">{children}</div>
    </header>
  )
}
