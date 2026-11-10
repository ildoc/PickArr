export function formatDate(value: string | null | undefined, locale = 'en'): string {
  if (!value) return '—'
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return '—'
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium' }).format(date)
}

export function formatYear(year: number | null | undefined): string {
  return year ? String(year) : '—'
}

export function formatRuntime(minutes: number | null | undefined, locale = 'en'): string {
  if (!minutes) return '—'
  return new Intl.NumberFormat(locale, { style: 'unit', unit: 'minute', unitDisplay: 'short' }).format(minutes)
}

export function categoryLabelKey(category: string): string {
  switch (category) {
    case 'movie':
      return 'category.movie'
    case 'series':
      return 'category.series'
    case 'standup':
      return 'category.standup'
    default:
      return 'category.unknown'
  }
}

export function formatBytes(bytes: number | null | undefined, locale = 'en'): string {
  if (bytes == null) return '—'
  const units = ['B', 'KB', 'MB', 'GB', 'TB']
  let value = bytes
  let unit = 0
  while (value >= 1024 && unit < units.length - 1) {
    value /= 1024
    unit++
  }
  const formatted = new Intl.NumberFormat(locale, {
    maximumFractionDigits: value >= 10 || unit === 0 ? 0 : 1,
  }).format(value)
  return `${formatted} ${units[unit]}`
}

export function arrStateLabelKey(state: string): string {
  switch (state) {
    case 'notInLibrary':
      return 'arr.notInLibrary'
    case 'missing':
      return 'arr.missing'
    case 'downloading':
      return 'arr.downloading'
    case 'downloaded':
      return 'arr.downloaded'
    case 'partial':
      return 'arr.partial'
    default:
      return 'arr.unknown'
  }
}
