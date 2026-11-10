import { describe, expect, it } from 'vitest'
import {
  arrStateLabelKey,
  categoryLabelKey,
  formatBytes,
  formatDate,
  formatDateTime,
  formatYear,
} from './format'

describe('format helpers', () => {
  it('maps categories and arr states', () => {
    expect(categoryLabelKey('standup')).toBe('category.standup')
    expect(arrStateLabelKey('notInLibrary')).toBe('arr.notInLibrary')
    expect(formatYear(null)).toBe('—')
    expect(formatYear(2024)).toBe('2024')
  })

  it('formats byte sizes', () => {
    expect(formatBytes(null)).toBe('—')
    expect(formatBytes(512)).toBe('512 B')
    expect(formatBytes(1024)).toBe('1 KB')
  })

  it('formats dates with and without time', () => {
    expect(formatDate(null)).toBe('—')
    expect(formatDateTime(null)).toBe('—')
    const value = '2026-11-10T14:30:00.000Z'
    const dateOnly = formatDate(value, 'en-GB')
    const withTime = formatDateTime(value, 'en-GB')
    expect(dateOnly).toMatch(/10/)
    expect(withTime).toContain(dateOnly)
    expect(withTime.length).toBeGreaterThan(dateOnly.length)
  })
})

