import { Navigate, Route, Routes, useSearchParams } from 'react-router-dom'
import { AppShell } from '../components/layout/AppShell'
import { JobsPage } from '../features/admin/JobsPage'
import { CatalogPage } from '../features/catalog/CatalogPage'
import { MyListPage } from '../features/list/MyListPage'
import { MyRatingsPage } from '../features/ratings/MyRatingsPage'
import { ForYouPage } from '../features/reco/ForYouPage'
import { TastePage } from '../features/reco/TastePage'
import { CatalogSourcesPage } from '../features/settings/CatalogSourcesPage'
import { RecommendationsSettingsPage } from '../features/settings/RecommendationsSettingsPage'
import { SettingsLayout } from '../features/settings/SettingsLayout'
import { SettingsPage } from '../features/settings/SettingsPage'

function CatalogRoute() {
  const [params] = useSearchParams()
  if (params.get('view') !== 'favorites') return <CatalogPage />

  const next = new URLSearchParams(params)
  next.delete('view')
  next.delete('source')
  const search = next.toString()
  return <Navigate to={search ? `/list?${search}` : '/list'} replace />
}

export function AppRouter() {
  return (
    <Routes>
      <Route element={<AppShell />}>
        <Route index element={<Navigate to="/catalog" replace />} />
        <Route path="catalog" element={<CatalogRoute />} />
        <Route path="for-you" element={<ForYouPage />} />
        <Route path="taste" element={<TastePage />} />
        <Route path="list" element={<MyListPage />} />
        <Route path="ratings" element={<MyRatingsPage />} />
        <Route path="admin/jobs" element={<Navigate to="/settings/jobs" replace />} />
        <Route path="settings" element={<SettingsLayout />}>
          <Route index element={<SettingsPage />} />
          <Route path="catalogs" element={<CatalogSourcesPage />} />
          <Route path="recommendations" element={<RecommendationsSettingsPage />} />
          <Route path="jobs" element={<JobsPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/catalog" replace />} />
    </Routes>
  )
}
