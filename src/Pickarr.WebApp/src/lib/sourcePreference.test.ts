import { beforeEach, describe, expect, it, vi } from 'vitest'
import { loadPreferredSourceId, savePreferredSourceId } from './sourcePreference'

describe('sourcePreference', () => {
  beforeEach(() => {
    const store = new Map<string, string>()
    vi.stubGlobal('localStorage', {
      getItem: (key: string) => store.get(key) ?? null,
      setItem: (key: string, value: string) => {
        store.set(key, value)
      },
      removeItem: (key: string) => {
        store.delete(key)
      },
      clear: () => {
        store.clear()
      },
    })
  })

  it('round-trips a preferred source id for one profile', () => {
    expect(loadPreferredSourceId(4, true)).toBeNull()
    savePreferredSourceId(4, 42)
    expect(loadPreferredSourceId(4, true)).toBe(42)
    expect(loadPreferredSourceId(5, false)).toBeNull()
  })

  it('copies the legacy key once for the admin profile', () => {
    localStorage.setItem('pickarr.sourceId', '7')
    expect(loadPreferredSourceId(1, true)).toBe(7)
    expect(localStorage.getItem('pickarr.sourceId.1')).toBe('7')
    localStorage.setItem('pickarr.sourceId', '9')
    expect(loadPreferredSourceId(1, true)).toBe(7)
    expect(loadPreferredSourceId(2, false)).toBeNull()
  })

  it('returns null for non-numeric storage values', () => {
    localStorage.setItem('pickarr.sourceId.4', 'nope')
    expect(loadPreferredSourceId(4, true)).toBeNull()
  })
})
