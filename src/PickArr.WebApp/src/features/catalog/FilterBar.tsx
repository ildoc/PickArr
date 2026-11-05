import { useTranslation } from 'react-i18next'
import type { CatalogSource, CatalogTypeCount, GenreCount } from '../../api/types'
import { QuickFilters } from './QuickFilters'

export function FilterBar({
  q,
  onQChange,
  view,
  onViewChange,
  sort,
  onSortChange,
  total,
  sources,
  countrySourceId,
  onCountryChange,
  type,
  onTypeChange,
  types,
  genre,
  genres,
  onGenreChange,
  dir,
  onDirChange,
  hideSource = false,
  hideViews = false,
}: {
  q: string
  onQChange: (value: string) => void
  view: string
  onViewChange: (view: string) => void
  sort: string
  onSortChange: (sort: string) => void
  total: number | undefined
  sources: CatalogSource[]
  countrySourceId: number | null
  onCountryChange: (sourceId: number) => void
  type: string
  onTypeChange: (type: string) => void
  types: CatalogTypeCount[]
  genre: string
  genres: GenreCount[]
  onGenreChange: (genre: string) => void
  dir: 'asc' | 'desc'
  onDirChange: (dir: 'asc' | 'desc') => void
  hideSource?: boolean
  hideViews?: boolean
}) {
  const { t } = useTranslation()
  const sortOptions = q.trim()
    ? ['relevance', 'added', 'year', 'rating', 'title']
    : ['added', 'year', 'rating', 'title']
  const selected = sources.find((source) => source.id === countrySourceId) ?? sources[0]
  const providers = [...new Map(sources.map((source) => [source.providerKey, source.providerName])).entries()]
  const countries = sources.filter((source) => source.providerKey === selected?.providerKey)

  const onProviderChange = (providerKey: string) => {
    const sameCountry = sources.find(
      (source) => source.providerKey === providerKey && source.countryCode === selected?.countryCode,
    )
    const next = sameCountry ?? sources.find((source) => source.providerKey === providerKey)
    if (next) onCountryChange(next.id)
  }

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        {hideSource ? (
          <span className="text-sm text-[var(--muted)]">{t('catalog.crossCatalog')}</span>
        ) : sources.length === 0 ? (
          <span className="text-sm text-[var(--muted)]">{t('catalog.noSources')}</span>
        ) : (
          <>
            <label className="flex items-center gap-2 text-sm text-[var(--muted)]">
              {t('catalog.provider')}
              <select
                className="rounded-md border border-white/10 bg-[var(--surface-2)] px-2 py-2 text-[var(--text)]"
                value={selected?.providerKey ?? ''}
                onChange={(e) => onProviderChange(e.target.value)}
              >
                {providers.map(([key, name]) => (
                  <option key={key} value={key}>
                    {name}
                  </option>
                ))}
              </select>
            </label>
            <label className="flex items-center gap-2 text-sm text-[var(--muted)]">
              {t('catalog.country')}
              <select
                className="rounded-md border border-white/10 bg-[var(--surface-2)] px-2 py-2 text-[var(--text)]"
                value={countrySourceId ?? ''}
                onChange={(e) => onCountryChange(Number(e.target.value))}
              >
                {countries.map((source) => (
                  <option key={source.id} value={source.id}>
                    {t(`country.${source.countryCode}`, { defaultValue: source.countryCode })}
                    {source.dataStale ? ` (${t('catalog.stale')})` : ''}
                  </option>
                ))}
              </select>
            </label>
          </>
        )}
        <label className="flex items-center gap-2 text-sm text-[var(--muted)]">
          {t('catalog.type')}
          <select
            className="rounded-md border border-white/10 bg-[var(--surface-2)] px-2 py-2 text-[var(--text)]"
            value={type}
            onChange={(e) => onTypeChange(e.target.value)}
          >
            <option value="">{t('catalog.typeAll')}</option>
            {types.map((item) => (
              <option key={item.type} value={item.type}>
                {t(`catalog.types.${item.type}`, { defaultValue: item.type })}
              </option>
            ))}
          </select>
        </label>
        <label className="flex items-center gap-2 text-sm text-[var(--muted)]">
          {t('catalog.genre')}
          <select
            className="rounded-md border border-white/10 bg-[var(--surface-2)] px-2 py-2 text-[var(--text)]"
            value={genre}
            onChange={(e) => onGenreChange(e.target.value)}
          >
            <option value="">{t('catalog.genreAll')}</option>
            {genres.map((item) => (
              <option key={item.id} value={item.id}>
                {item.name}
              </option>
            ))}
          </select>
        </label>
        <input
          type="search"
          value={q}
          onChange={(e) => onQChange(e.target.value)}
          placeholder={t('catalog.searchPlaceholder')}
          className="min-w-[280px] flex-1 rounded-md border border-white/10 bg-[var(--surface-2)] px-3 py-2 text-[var(--text)] placeholder:text-[var(--muted)]"
        />
        <label className="flex items-center gap-2 text-sm text-[var(--muted)]">
          {t('sort.label')}
          <select
            className="rounded-md border border-white/10 bg-[var(--surface-2)] px-2 py-2 text-[var(--text)]"
            value={sort}
            onChange={(e) => onSortChange(e.target.value)}
          >
            {sortOptions.map((opt) => (
              <option key={opt} value={opt}>
                {t(`sort.${opt}`)}
              </option>
            ))}
          </select>
          <button
            type="button"
            className="rounded-md border border-white/10 bg-[var(--surface-2)] px-2 py-2 text-[var(--text)]"
            aria-label={t('sort.direction')}
            onClick={() => onDirChange(dir === 'asc' ? 'desc' : 'asc')}
          >
            {dir === 'asc' ? t('sort.asc') : t('sort.desc')}
          </button>
        </label>
        {total != null && (
          <span className="text-sm text-[var(--muted)]">
            {t('catalog.total', { count: total })}
          </span>
        )}
      </div>
      {hideViews ? null : <QuickFilters value={view} onChange={onViewChange} />}
    </div>
  )
}
