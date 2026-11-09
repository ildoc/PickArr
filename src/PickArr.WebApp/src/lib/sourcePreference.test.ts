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

  it('round-trips a preferred source id', () => {
    expect(loadPreferredSourceId()).toBeNull()
    savePreferredSourceId(42)
    expect(loadPreferredSourceId()).toBe(42)
  })

  it('returns null for non-numeric storage values', () => {
    localStorage.setItem('pickarr.sourceId', 'nope')
    expect(loadPreferredSourceId()).toBeNull()
  })
})
