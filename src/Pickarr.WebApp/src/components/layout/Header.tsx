import type { ReactNode } from 'react'
import { useQuery } from '@tanstack/react-query'
import { NavLink, useLocation } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { fetchSettings } from '../../api/settings'
import { fetchSystemStatus } from '../../api/system'
import { profileInitial, profileSwatch } from '../../features/who/colors'
import { useProfiles } from '../../features/who/profileContext'
import kofiLogo from '../../assets/kofiLogo.webp'

const REPO_URL = 'https://github.com/ildoc/Pickarr'
const KOFI_URL = 'https://ko-fi.com/pheeleeppo'

const linkClass = ({ isActive }: { isActive: boolean }) =>
  `text-sm ${isActive ? 'text-[var(--text)]' : 'text-[var(--muted)] hover:text-[var(--text)]'}`

export function Header({ children }: { children?: ReactNode }) {
  const { t } = useTranslation()
  const location = useLocation()
  const onSetup = location.pathname === '/setup'
  const { active } = useProfiles()
  const settings = useQuery({ queryKey: ['settings'], queryFn: fetchSettings, enabled: !onSetup })
  const system = useQuery({
    queryKey: ['system-status'],
    queryFn: fetchSystemStatus,
    staleTime: Infinity,
  })
  const recoEnabled = settings.data?.reco.enabled !== false
  const version = system.data?.version

  const brand = (
    <>
      <img
        src="/logo.png"
        alt=""
        width={32}
        height={32}
        className="h-8 w-8 shrink-0 rounded-full"
      />
      <span>{t('app.title')}</span>
    </>
  )

  return (
    <header className="sticky top-0 z-40 flex items-center gap-8 border-b border-white/10 bg-[var(--surface)]/95 px-8 py-4 backdrop-blur-sm">
      <span className="inline-flex items-center gap-2 text-xl font-semibold tracking-tight text-[var(--text)]">
        {onSetup ? (
          brand
        ) : (
          <NavLink to="/catalog" className="inline-flex items-center gap-2 text-[var(--text)]">
            {brand}
          </NavLink>
        )}
      </span>
      {!onSetup && (
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
          {active?.isAdmin && (
            <NavLink to="/settings/system" className={linkClass}>
              {t('nav.settings')}
            </NavLink>
          )}
        </nav>
      )}
      <div className="ml-auto flex items-center gap-4">
        {!onSetup && (
          <NavLink
            to="/who"
            aria-label={t('who.open', { name: active?.name ?? t('who.heading') })}
            title={t('who.open', { name: active?.name ?? t('who.heading') })}
            className="inline-flex items-center gap-2 text-sm font-medium text-[var(--text)]"
          >
            <span
              aria-hidden
              className="grid h-8 w-8 shrink-0 place-items-center rounded-full text-sm font-semibold text-black"
              style={{ background: profileSwatch(active?.color ?? 'blue') }}
            >
              {active ? profileInitial(active.name) : '?'}
            </span>
            {active && <span className="max-w-32 truncate">{active.name}</span>}
          </NavLink>
        )}
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
          className="inline-flex opacity-90 transition hover:opacity-100"
        >
          <img src={kofiLogo} alt="" width={72} height={24} className="h-6 w-auto" />
        </a>
      </div>
    </header>
  )
}
