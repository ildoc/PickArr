export const profilePalette = ['blue', 'teal', 'green', 'yellow', 'orange', 'red', 'pink', 'purple'] as const

export type ProfileColor = (typeof profilePalette)[number]

const swatches: Record<string, string> = {
  blue: '#60a5fa',
  teal: '#2dd4bf',
  green: '#4ade80',
  yellow: '#facc15',
  orange: '#fb923c',
  red: '#f87171',
  pink: '#f472b6',
  purple: '#c084fc',
}

export function profileSwatch(color: string): string {
  return swatches[color] ?? swatches.blue
}

export function profileInitial(name: string): string {
  const letter = name.trim().charAt(0)
  return letter ? letter.toUpperCase() : '?'
}
