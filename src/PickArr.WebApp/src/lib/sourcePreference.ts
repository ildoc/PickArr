const KEY = 'pickarr.sourceId'

export function loadPreferredSourceId(): number | null {
  const raw = localStorage.getItem(KEY)
  if (!raw) return null
  const id = Number(raw)
  return Number.isFinite(id) ? id : null
}

export function savePreferredSourceId(id: number): void {
  localStorage.setItem(KEY, String(id))
}
