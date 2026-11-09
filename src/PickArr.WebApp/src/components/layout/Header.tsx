import type { ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { NavLink } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { fetchSettings } from '../../api/settings'
import { fetchSystemStatus } from '../../api/system'

const REPO_URL = 'https://github.com/ildoc/Pickarr'
const KOFI_URL = 'https://ko-fi.com/pheeleeppo'

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `text-sm ${isActive ? 'text-[var(--text)]' : 'text-[var(--muted)] hover:text-[var(--text)]'}`

function KofiIcon({ className }: { className?: string }) {
  return (
    <svg
      className={className}
      viewBox="0 0 24 24"
      width="18"
      height="18"
      aria-hidden
      fill="currentColor"
    >
      <path d="M23.881 8.948c-.773-4.085-4.859-4.593-4.859-4.593H.723c-.604 0-.697.776-.697.776s-.094 7.241-.029 11.396c.115 2.294 1.928 2.903 1.928 2.903s6.198.392 10.209.392c4.288 0 5.478-.233 6.43-1.418.949-1.184.928-3.237.928-3.237 4.823-.003 6.786-3.32 5.389-5.819zM2.846 18.614c-.115-1.385-.145-5.861-.145-5.861h8.985s.113 5.369-.029 6.195c-.188 1.077-1.077.962-1.077.962H3.877s-.924-.039-1.031-.296zm18.373-7.337c-1.924.115-3.154-.928-3.154-.928s.113-2.464.928-3.154c.814-.693 2.348-.464 3.154.232.808.695.693 2.541-.928 3.85z" />
    </svg>
  )
}

export function Header({ children }: { children?: ReactNode }) {
  const { t } = useTranslation()
  const settings = useQuery({ queryKey: ['settings'], queryFn: fetchSettings })
  const system = useQuery({
    queryKey: ['system-status'],
    queryFn: fetchSystemStatus,
    staleTime: Infinity,
  })
  const recoEnabled = settings.data?.reco.enabled !== false
  const version = system.data?.version

  return (
    <header className="sticky top-0 z-40 flex items-center gap-8 border-b border-white/10 bg-[var(--surface)]/95 px-8 py-4 backdrop-blur-sm">
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
      <div className="ml-auto flex items-center gap-4">
        {children}
        {version && (
          <a
            href={REPO_URL}
            target="_blank"
            rel="noreferrer"
            className="text-xs tabular-nums text-[var(--muted)] underline decoration-white/20 hover:text-[var(--text)]"
            title={t('app.repoLink')}
          >
            {version}
          </a>
        )}
        <a
          href={KOFI_URL}
          target="_blank"
          rel="noreferrer"
          aria-label={t('app.kofiLink')}
          title={t('app.kofiLink')}
          className="text-[#ff5e5b] transition hover:text-[#ff7b78]"
        >
          <KofiIcon />
        </a>
      </div>
    </header>
  )
}
