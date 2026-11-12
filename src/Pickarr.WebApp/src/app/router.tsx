import { useQuery } from '@tanstack/react-query'
import { Navigate, Outlet, Route, Routes, useLocation, useSearchParams } from 'react-router-dom'
import { fetchSetupStatus } from '../api/setup'
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
import { SystemPage } from '../features/settings/SystemPage'
import { SetupPage } from '../features/setup/SetupPage'
import { WhoPage } from '../features/who/WhoPage'
import { ProfileProvider, useProfiles } from '../features/who/profileContext'

function CatalogRoute() {
  const [params] = useSearchParams()
  if (params.get('view') !== 'favorites') return <CatalogPage />

  const next = new URLSearchParams(params)
  next.delete('view')
  next.delete('source')
  const search = next.toString()
  return <Navigate to={search ? `/list?${search}` : '/list'} replace />
}

function SetupGate() {
  const location = useLocation()
  const setup = useQuery({ queryKey: ['setup'], queryFn: fetchSetupStatus, staleTime: 30_000 })
  if (!setup.isSuccess) return null
  const onSetup = location.pathname === '/setup'
  if (!setup.data.completed && !onSetup) return <Navigate to="/setup" replace />
  if (setup.data.completed && onSetup) return <Navigate to="/catalog" replace />
  return <Outlet />
}

function ProfileGate() {
  const location = useLocation()
  const { active, ready } = useProfiles()
  if (!ready) return null
  const onWho = location.pathname === '/who'
  const onSetup = location.pathname === '/setup'
  if (!active && !onWho && !onSetup) return <Navigate to="/who" replace />
  if (active && !active.isAdmin && location.pathname.startsWith('/settings')) {
    return <Navigate to="/catalog" replace />
  }
  return <Outlet />
}

export function AppRouter() {
  return (
    <ProfileProvider>
      <Routes>
        <Route element={<SetupGate />}>
          <Route element={<AppShell />}>
            <Route path="setup" element={<SetupPage />} />
            <Route element={<ProfileGate />}>
              <Route index element={<Navigate to="/catalog" replace />} />
              <Route path="catalog" element={<CatalogRoute />} />
              <Route path="who" element={<WhoPage />} />
              <Route path="for-you" element={<ForYouPage />} />
              <Route path="taste" element={<TastePage />} />
              <Route path="list" element={<MyListPage />} />
              <Route path="ratings" element={<MyRatingsPage />} />
              <Route path="admin/jobs" element={<Navigate to="/settings/jobs" replace />} />
              <Route path="settings" element={<SettingsLayout />}>
                <Route index element={<Navigate to="/settings/connections" replace />} />
                <Route path="system" element={<SystemPage />} />
                <Route path="users" element={<WhoPage mode="management" />} />
                <Route path="catalogs" element={<CatalogSourcesPage />} />
                <Route path="recommendations" element={<RecommendationsSettingsPage />} />
                <Route path="connections" element={<SettingsPage />} />
                <Route path="jobs" element={<JobsPage />} />
              </Route>
            </Route>
          </Route>
        </Route>
        <Route path="*" element={<Navigate to="/catalog" replace />} />
      </Routes>
    </ProfileProvider>
  )
}
