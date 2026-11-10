const legacyKey = 'pickarr.sourceId'

export function sourcePreferenceKey(profileId: number): string {
  return `${legacyKey}.${profileId}`
}

function readId(key: string): number | null {
  const raw = localStorage.getItem(key)
  if (!raw) return null
  const id = Number(raw)
  return Number.isFinite(id) ? id : null
}

/** Per-profile catalog source. An admin without one copies the legacy key once. */
export function loadPreferredSourceId(profileId: number, isAdmin: boolean): number | null {
  const own = readId(sourcePreferenceKey(profileId))
  if (own != null) return own
  if (!isAdmin) return null
  const legacy = readId(legacyKey)
  if (legacy == null) return null
  localStorage.setItem(sourcePreferenceKey(profileId), String(legacy))
  return legacy
}

export function savePreferredSourceId(profileId: number, id: number): void {
  localStorage.setItem(sourcePreferenceKey(profileId), String(id))
}
