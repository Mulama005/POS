import type { ReactNode } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import type { UserRole } from '../types/auth'

/**
 * Wrap a group of routes with this (as a layout route) to require any
 * logged-in user. Redirects to /login, preserving the attempted location in
 * router state so LoginPage could send them back afterward if it wanted to
 * (not wired up yet — the current redirect always goes to the role's
 * landing route, not back to what was attempted; that's a reasonable future
 * enhancement, not a Step 12 requirement).
 */
export function RequireAuth() {
  const { status } = useAuth()
  const location = useLocation()

  if (status === 'loading') {
    return <div className="route-guard-loading">Loading…</div>
  }
  if (status === 'unauthenticated') {
    return <Navigate to="/login" state={{ from: location }} replace />
  }
  return <Outlet />
}

/**
 * Wrap a group of routes with this to additionally require one of the given
 * roles — e.g. only Manager/Admin can reach /mfa/setup. Sends anyone
 * authenticated-but-wrong-role to /forbidden rather than /login, since
 * bouncing a legitimately logged-in Cashier back to the login screen would
 * be a confusing dead end.
 */
export function RequireRole({ roles }: { roles: readonly UserRole[] }) {
  const { status, user } = useAuth()
  const location = useLocation()

  if (status === 'loading') {
    return <div className="route-guard-loading">Loading…</div>
  }
  if (status === 'unauthenticated' || !user) {
    return <Navigate to="/login" state={{ from: location }} replace />
  }
  if (!roles.includes(user.role)) {
    return <Navigate to="/forbidden" replace />
  }
  return <Outlet />
}

/**
 * Component-level guard for hiding a specific piece of UI (a button, a nav
 * link) that a role shouldn't see — distinct from RequireRole, which blocks
 * an entire route/page. Use this inline: <RoleGate roles={['Manager','Admin']}>
 * <button>Void sale</button></RoleGate>. Renders nothing (or `fallback`) for
 * anyone without a matching role.
 */
export function RoleGate({
  roles,
  fallback = null,
  children,
}: {
  roles: readonly UserRole[]
  fallback?: ReactNode
  children: ReactNode
}) {
  const { user } = useAuth()
  if (!user || !roles.includes(user.role)) {
    return <>{fallback}</>
  }
  return <>{children}</>
}