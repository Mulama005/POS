import { Navigate, Route, Routes } from 'react-router-dom'
import { LoginPage } from './pages/LoginPage'
import { CheckoutPage } from './pages/CheckoutPage'
import { MfaSetupPage } from './pages/MfaSetupPage'
import { ForbiddenPage } from './pages/ForbiddenPage'
import { UserManagementPage } from './pages/UserManagementPage'
import { AcceptInvitePage } from './pages/AcceptInvitePage'
import { RepairsPage } from './pages/RepairsPage'
import { CustomersPage } from './pages/CustomersPage'
import { RepairTrackingPage } from './pages/RepairTrackingPage'
import { ProductsPage } from './pages/ProductsPage'
import { ReceiveStock } from './pages/ReceiveStock'
import { WarrantyLookupPage } from './pages/WarrantyLookupPage'
import { AdminDashboard } from './pages/AdminDashboard'
import { ManagerDashboard } from './pages/ManagerDashboard'
import { DashboardLayout } from './layouts/DashboardLayouts'
import { RequireAuth, RequireRole } from './components/RouteGuards'
import { useAuth } from './hooks/useAuth'
import type { UserRole } from './types/auth'

// Where each role lands after login / on an unmatched URL. Keep this in
// sync with DashboardLayouts.tsx's HOME_PATH — that one decides where the
// sidebar logo links to, this one decides where auth lands you.
const HOME_PATH: Record<UserRole, string> = {
  Cashier: '/checkout',
  Manager: '/dashboard/manager',
  Admin: '/dashboard/admin',
  Technician: '/repairs',
}

function RootRedirect() {
  const { status, user } = useAuth()
  if (status === 'loading') return <div>Loading…</div>
  if (status !== 'authenticated' || !user) return <Navigate to="/login" replace />
  return <Navigate to={HOME_PATH[user.role]} replace />
}

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/forbidden" element={<ForbiddenPage />} />
      <Route path="/accept-invite" element={<AcceptInvitePage />} />
      <Route path="/track-repair" element={<RepairTrackingPage />} />

      <Route element={<RequireAuth />}>
        <Route element={<DashboardLayout />}>
          <Route path="/checkout" element={<CheckoutPage />} />
          <Route path="/repairs" element={<RepairsPage />} />
          <Route path="/customers" element={<CustomersPage />} />
          {/* No role restriction — matches StockController's WarrantyLookup endpoint, which
              is plain [Authorize]. Any signed-in role can answer a warranty question. */}
          <Route path="/warranty-lookup" element={<WarrantyLookupPage />} />
        </Route>
      </Route>

      {/* Admin can also view the Manager dashboard — Manager capabilities are a subset of
          Admin's everywhere else in the app, no reason this view should be the exception. */}
      <Route element={<RequireRole roles={['Manager', 'Admin']} />}>
        <Route element={<DashboardLayout />}>
          <Route path="/dashboard/manager" element={<ManagerDashboard />} />
          <Route path="/mfa/setup" element={<MfaSetupPage />} />
          <Route path="/inventory" element={<ProductsPage />} />
          <Route path="/stock/receive" element={<ReceiveStock />} />
        </Route>
      </Route>

      <Route element={<RequireRole roles={['Admin']} />}>
        <Route element={<DashboardLayout />}>
          <Route path="/dashboard/admin" element={<AdminDashboard />} />
          <Route path="/users" element={<UserManagementPage />} />
        </Route>
      </Route>

      <Route path="/" element={<RootRedirect />} />
      {/* Catch-all — an unmatched URL used to render a blank page. Send it through the same
          role-aware redirect as "/" instead of leaving the user stranded. */}
      <Route path="*" element={<RootRedirect />} />
    </Routes>
  )
}

export default App