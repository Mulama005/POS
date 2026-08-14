import { Navigate, Route, Routes } from 'react-router-dom'
import { LoginPage } from './pages/LoginPage'
import { PlaceholderPage } from './pages/PlaceholderPage'
import { MfaSetupPage } from './pages/MfaSetupPage'
import { ForbiddenPage } from './pages/ForbiddenPage'
import { RequireAuth, RequireRole } from './components/RouteGuards'
import { AuthProvider } from './store/AuthContext'   // ← add this

function App() {
  return (
    <AuthProvider>                                    {/* ← wrap everything */}
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/forbidden" element={<ForbiddenPage />} />

        <Route element={<RequireAuth />}>
          <Route path="/checkout" element={<PlaceholderPage title="Checkout" />} />
          <Route path="/repairs" element={<PlaceholderPage title="Repairs queue" />} />
        </Route>

        <Route element={<RequireRole roles={['Manager']} />}>
          <Route path="/dashboard/manager" element={<PlaceholderPage title="Manager dashboard" />} />
        </Route>
        <Route element={<RequireRole roles={['Admin']} />}>
          <Route path="/dashboard/admin" element={<PlaceholderPage title="Admin dashboard" />} />
        </Route>

        <Route element={<RequireRole roles={['Manager', 'Admin']} />}>
          <Route path="/mfa/setup" element={<MfaSetupPage />} />
        </Route>

        <Route path="/" element={<Navigate to="/login" replace />} />
        <Route path="*" element={<Navigate to="/login" replace />} />
      </Routes>
    </AuthProvider>
  )
}

export default App