import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { RoleGate } from '../components/RouteGuards'

/**
 * TEMPORARY landing screen. Step 11 only needs somewhere real to redirect to
 * after login/MFA succeeds — the actual checkout, dashboard, and repairs
 * queue screens are later steps (checkout: Phase 3; dashboards: Phase 5/6;
 * repairs queue: Phase 4). Replace each usage of this component with the
 * real screen as its step comes up; this file itself can be deleted once
 * nothing imports it anymore.
 */
export function PlaceholderPage({ title }: { title: string }) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  const handleSignOut = async () => {
    await logout()
    // Explicit navigation here, not something Step 12's route guards will
    // cover — a guard redirects someone AWAY from a protected route on
    // load/navigation, but doesn't fire just because state changed while
    // already sitting on the page. The sign-out action itself has to send
    // the user somewhere, guard or no guard.
    navigate('/login', { replace: true })
  }

  return (
    <div style={{ padding: 40, fontFamily: 'var(--pos-font-body)', color: 'var(--pos-ink)' }}>
      <h1 style={{ fontFamily: 'var(--pos-font-display)' }}>{title}</h1>
      <p style={{ color: 'var(--pos-ink-muted)' }}>
        Signed in as {user?.fullName} ({user?.role}). This screen hasn't been built yet.
      </p>

      {/* Optional, per the plan — MFA is never forced, just offered. Only
          shown to roles that can even use it, and only if not already on. */}
      <RoleGate roles={['Manager', 'Admin']}>
        {user && !user.mfaEnabled && (
          <p
            style={{
              marginTop: 16,
              padding: '10px 14px',
              background: 'var(--pos-accent-soft)',
              border: '1px solid var(--pos-accent)',
              borderRadius: 6,
              fontSize: 13.5,
            }}
          >
            Two-factor authentication isn't enabled on your account.{' '}
            <Link to="/mfa/setup" style={{ color: 'var(--pos-accent)', fontWeight: 600 }}>
              Set it up
            </Link>
            {' '}(optional, but recommended for this role).
          </p>
        )}
      </RoleGate>

      <button
        type="button"
        onClick={() => void handleSignOut()}
        style={{
          marginTop: 16,
          padding: '8px 14px',
          border: '1px solid var(--pos-border)',
          borderRadius: 6,
          background: 'var(--pos-surface)',
          cursor: 'pointer',
        }}
      >
        Sign out
      </button>
    </div>
  )
}