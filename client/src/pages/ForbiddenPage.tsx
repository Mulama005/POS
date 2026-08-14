import { Link } from 'react-router-dom'

export function ForbiddenPage() {
  return (
    <div style={{ padding: 40, fontFamily: 'var(--pos-font-body)', color: 'var(--pos-ink)' }}>
      <h1 style={{ fontFamily: 'var(--pos-font-display)' }}>Access denied</h1>
      <p style={{ color: 'var(--pos-ink-muted)' }}>
        Your account doesn't have permission to view this page.
      </p>
      <Link to="/login" style={{ color: 'var(--pos-accent)', fontWeight: 600 }}>
        Back to sign in
      </Link>
    </div>
  )
}