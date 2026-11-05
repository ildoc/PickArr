import { useTranslation } from 'react-i18next'

export function Attributions() {
  const { t } = useTranslation()

  return (
    <div className="flex flex-wrap items-center gap-6 text-sm text-[var(--muted)]">
      <a
        className="underline decoration-white/20 hover:text-[var(--text)]"
        href="https://watchmode.com"
        target="_blank"
        rel="noreferrer"
      >
        {t('attributions.watchmode')}
      </a>
      <div className="flex max-w-xl items-center gap-3">
        <span
          aria-hidden
          className="rounded bg-[#01b4e4]/20 px-1.5 py-0.5 text-[10px] font-bold tracking-wide text-[#01b4e4]"
        >
          TMDB
        </span>
        <span>{t('attributions.tmdb')}</span>
      </div>
    </div>
  )
}
