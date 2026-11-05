import { describe, expect, it } from 'vitest'
import { arrStateLabelKey, categoryLabelKey, formatBytes, formatYear } from './format'

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
})
