import { Outlet } from 'react-router-dom'
import { useTranslation } from 'react-i18next'
import { Attributions } from './Attributions'
import { Header } from './Header'

export function AppShell() {
  const { t } = useTranslation()

  return (
    <div className="flex min-h-screen flex-col">
      <p className="bg-amber-950/80 px-4 py-1 text-center text-xs text-amber-200 min-[1280px]:hidden">
        {t('widthWarning')}
      </p>
      <Header />
      <main className="mx-auto w-full max-w-[1600px] flex-1 px-8 py-6">
        <Outlet />
      </main>
      <footer className="border-t border-white/10 px-8 py-4">
        <div className="mx-auto flex max-w-[1600px] items-center justify-between gap-4">
          <Attributions />
          <span className="text-xs text-[var(--muted)]">{t('app.version')}</span>
        </div>
      </footer>
    </div>
  )
}
