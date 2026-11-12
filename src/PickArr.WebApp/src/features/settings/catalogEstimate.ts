export type EstimateCountry = {
  code: string
  selected: boolean
  pages: number
  incrementalPages: number
  fullyImported: boolean
}

export type EstimateInput = {
  countries: EstimateCountry[]
  used: number
  quota: number
  freePlan: boolean
  freeCountryLimit: number
  creditsPerCall: number
  fullSyncsPerMonth: number
  dailyRunsPerMonth: number
}

export type CatalogEstimate = {
  countries: number
  catalogs: number
  fill: number
  monthly: number
  overQuota: boolean
  overCountryLimit: boolean
}

export function estimateCatalog(input: EstimateInput): CatalogEstimate {
  const selected = input.countries.filter((country) => country.selected)
  const countries = new Set(selected.map((country) => country.code.trim().toUpperCase())).size
  const fill = selected
    .filter((country) => !country.fullyImported)
    .reduce((sum, country) => sum + country.pages * input.creditsPerCall, 0)
  const monthly = selected.reduce(
    (sum, country) =>
      sum +
      (country.incrementalPages * input.dailyRunsPerMonth +
        country.pages * input.fullSyncsPerMonth +
        input.dailyRunsPerMonth) *
        input.creditsPerCall,
    0,
  )

  return {
    countries,
    catalogs: selected.length,
    fill,
    monthly,
    overQuota:
      input.freePlan && (input.used + monthly > input.quota || input.used + fill > input.quota),
    overCountryLimit: input.freePlan && countries > input.freeCountryLimit,
  }
}
