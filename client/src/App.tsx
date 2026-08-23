import { Navigate, Route, Routes } from 'react-router-dom'
import { LoginPage } from './pages/LoginPage'
import { PlaceholderPage } from './pages/PlaceholderPage'
import { CheckoutPage } from './pages/CheckoutPage'
import { MfaSetupPage } from './pages/MfaSetupPage'
import { ForbiddenPage } from './pages/ForbiddenPage'
import { UserManagementPage } from './pages/UserManagementPage'
import { AcceptInvitePage } from './pages/AcceptInvitePage'
import { RepairsPage } from './pages/RepairsPage'
import { CustomersPage } from './pages/CustomersPage'
import { RepairTrackingPage } from './pages/RepairTrackingPage'
import { RequireAuth, RequireRole } from './components/RouteGuards'

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/forbidden" element={<ForbiddenPage />} />
      <Route path="/accept-invite" element={<AcceptInvitePage />} />
      <Route path="/track-repair" element={<RepairTrackingPage />} />

      <Route element={<RequireAuth />}>
        <Route path="/checkout" element={<CheckoutPage />} />
        <Route path="/repairs" element={<RepairsPage />} />
        <Route path="/customers" element={<CustomersPage />} />
      </Route>

      <Route element={<RequireRole roles={['Manager']} />}>
        <Route path="/dashboard/manager" element={<PlaceholderPage title="Manager dashboard" />} />
      </Route>

      <Route element={<RequireRole roles={['Admin']} />}>
        <Route path="/dashboard/admin" element={<PlaceholderPage title="Admin dashboard" />} />
        <Route path="/users" element={<UserManagementPage />} />
      </Route>

      <Route element={<RequireRole roles={['Manager', 'Admin']} />}>
        <Route path="/mfa/setup" element={<MfaSetupPage />} />
      </Route>

      <Route path="/" element={<Navigate to="/login" replace />} />
      <Route path="*" element={<Navigate to="/login" replace />} />
    </Routes>
  )
}

export default App