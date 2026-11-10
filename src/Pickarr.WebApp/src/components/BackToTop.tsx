import { useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'

const SHOW_AFTER_PX = 480

export function BackToTop() {
  const { t } = useTranslation()
  const [visible, setVisible] = useState(false)

  useEffect(() => {
    const update = () => setVisible(window.scrollY > SHOW_AFTER_PX)
    update()
    window.addEventListener('scroll', update, { passive: true })
    return () => window.removeEventListener('scroll', update)
  }, [])

  if (!visible) return null

  return (
    <button
      type="button"
      onClick={() => window.scrollTo({ top: 0, behavior: 'smooth' })}
      className="fixed bottom-6 right-6 z-30 rounded-md border border-white/15 bg-[var(--surface)]/95 px-3 py-2 text-sm font-medium text-[var(--text)] backdrop-blur-sm hover:bg-white/10"
    >
      {t('catalog.backToTop')}
    </button>
  )
}
