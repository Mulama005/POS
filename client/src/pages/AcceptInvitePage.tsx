import { useState, type FormEvent } from 'react'
import { useSearchParams, useNavigate, Link } from 'react-router-dom'
import { isAxiosError } from 'axios'
import { acceptInvite } from '../services/usersService'
import type { ApiErrorBody } from '../types/auth'
import './LoginPage.css' // reuses the login card styling deliberately — same "first thing a new user sees" visual language

function getErrorMessage(err: unknown, fallback: string): string {
  if (isAxiosError<ApiErrorBody>(err) && err.response?.data?.message) {
    return err.response.data.message
  }
  if (isAxiosError(err) && typeof err.response?.data === 'string') {
    return err.response.data
  }
  return fallback
}

export function AcceptInvitePage() {
  const [searchParams] = useSearchParams()
  const navigate = useNavigate()
  const userId = searchParams.get('userId')
  const token = searchParams.get('token')

  const [password, setPassword] = useState('')
  const [confirmPassword, setConfirmPassword] = useState('')
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [done, setDone] = useState(false)

  if (!userId || !token) {
    return (
      <div className="login-screen">
        <div className="login-card">
          <div className="login-card__header">
            <span className="login-wordmark">Ayiya<span className="login-wordmark__accent">POS</span></span>
          </div>
          <p className="login-error" role="alert">
            This invite link is missing required information. Ask whoever invited you to send a new one.
          </p>
        </div>
      </div>
    )
  }

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setError(null)

    if (password !== confirmPassword) {
      setError('Passwords do not match.')
      return
    }

    setSubmitting(true)
    try {
      await acceptInvite(userId, token, password)
      setDone(true)
      setTimeout(() => navigate('/login', { replace: true }), 1800)
    } catch (err) {
      // A common real cause here: invite tokens expire after 24 hours (set
      // server-side via Identity's password-reset token lifetime) — the
      // error message from the backend already explains this, no need to
      // guess at a friendlier one that might hide what actually happened.
      setError(getErrorMessage(err, 'Could not activate your account.'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="login-screen">
      <div className="login-card">
        <div className="login-card__header">
          <span className="login-wordmark">Ayiya<span className="login-wordmark__accent">POS</span></span>
          <p className="login-tagline">Set your password to get started</p>
        </div>

        {done ? (
          <p className="login-mfa-copy">Account activated — taking you to sign in…</p>
        ) : (
          <form className="login-form" onSubmit={(e) => void handleSubmit(e)} noValidate>
            <div className="login-field">
              <label htmlFor="password">New password</label>
              <input
                id="password"
                type="password"
                autoComplete="new-password"
                autoFocus
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
              />
            </div>

            <div className="login-field">
              <label htmlFor="confirmPassword">Confirm password</label>
              <input
                id="confirmPassword"
                type="password"
                autoComplete="new-password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                required
              />
            </div>

            {error && <p className="login-error" role="alert">{error}</p>}

            <button type="submit" className="login-submit" disabled={submitting}>
              {submitting ? 'Activating…' : 'Activate account'}
            </button>

            <Link to="/login" className="login-link-button" style={{ textAlign: 'center' }}>
              Back to sign in
            </Link>
          </form>
        )}
      </div>
    </div>
  )
}