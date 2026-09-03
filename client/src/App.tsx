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
import AdminDashboard from './pages/AdminDashboard'
import DashboardLayout from './layouts/DashboardLayouts'
import { RequireAuth, RequireRole } from './components/RouteGuards'
import ManagerDashboard from "./pages/ManagerDashboard.tsx"
import AuditLogPage from "./pages/AuditLogPage"
import ReportsPage from "./pages/ReportsPage"
import {useAuth} from "./hooks/useAuth.ts"

// Define a simple component
function RootRedirect() {
    const { status } = useAuth();
    if (status === "loading") return <div>Loading...</div>;
    return <Navigate to={status === "authenticated" ? "/checkout" : "/login"} replace />;
}

function App() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route path="/forbidden" element={<ForbiddenPage />} />
      <Route path="/accept-invite" element={<AcceptInvitePage />} />
      <Route path="/track-repair" element={<RepairTrackingPage />} />

        <Route element={<RequireAuth />}>
            <Route element={<DashboardLayout />}>   {/* <-- layout wrapper */}
                <Route path="/checkout" element={<CheckoutPage />} />
                <Route path="/repairs" element={<RepairsPage />} />
                <Route path="/customers" element={<CustomersPage />} />
            </Route>
        </Route>
        
        <Route element={<RequireRole roles={['Manager']} />}>
            <Route element={<DashboardLayout />}>
                <Route path="dashboard/manager" element={<ManagerDashboard />} />
            </Route>
        </Route>
        
        <Route element={<RequireRole roles={['Admin']} />}>
            <Route element={<DashboardLayout />}>
                <Route path="/dashboard/admin" element={<AdminDashboard />} />
                <Route path="/users" element={<UserManagementPage />} />
            </Route>
        </Route>
        
        <Route element={<RequireRole roles={['Manager', 'Admin']} />}>
            <Route element={<DashboardLayout />}>
                <Route path="/audit" element={<AuditLogPage />} />
                <Route path="/mfa/setup" element={<MfaSetupPage />} />
                <Route path="/reports" element={<ReportsPage />} />
            </Route>
        </Route>

        <Route path="/" element={<RootRedirect />} />
    </Routes>
  )
}

export default App