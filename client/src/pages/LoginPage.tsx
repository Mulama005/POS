import { useEffect, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { getLandingRouteForRole } from '../utils/roleRedirect'
import type { ApiErrorBody } from '../types/auth'
import { isAxiosError } from 'axios'
import { OtpInput } from '../components/OtpInput'
import './LoginPage.css'

const passwordStepSchema = z.object({
  email: z.string().min(1, 'Enter your email').email('Enter a valid email'),
  password: z.string().min(1, 'Enter your password'),
})
type PasswordStepValues = z.infer<typeof passwordStepSchema>

function getErrorMessage(err: unknown, fallback: string): string {
  if (isAxiosError<ApiErrorBody>(err) && err.response?.data?.message) {
    return err.response.data.message
  }
  return fallback
}

export function LoginPage() {
  const { status, user, loginStage, login, submitMfaCode, cancelMfaStep } = useAuth()
  const navigate = useNavigate()
  const [serverError, setServerError] = useState<string | null>(null)
  const [mfaSubmitting, setMfaSubmitting] = useState(false)

  useEffect(() => {
    if (status === 'loading') return;
    if (status === 'authenticated' && user) {
      navigate(getLandingRouteForRole(user.role), { replace: true })
    }
  }, [status, user, navigate])

  const passwordForm = useForm<PasswordStepValues>({
    resolver: zodResolver(passwordStepSchema),
  })

  const onSubmitPassword = async (values: PasswordStepValues) => {
    setServerError(null)
    try {
      await login(values.email, values.password)
      // On success, either the effect above fires (fully authenticated) or
      // loginStage flips to 'awaiting-mfa' and the screen below switches.
    } catch (err) {
      setServerError(getErrorMessage(err, 'Something went wrong. Try again.'))
    }
  }

  const handleOtpComplete = async (code: string) => {
    setServerError(null)
    setMfaSubmitting(true)
    try {
      await submitMfaCode(code)
    } catch (err) {
      // The challenge token is single-use (Step 10) — it's already burned
      // on this failed attempt, so send the person back to start rather
      // than letting them retry the same code. TOTP codes also aren't
      // something the server can "resend" (they're generated locally by the
      // authenticator app from a shared secret), so there's no resend
      // affordance here — restarting from the password step is the only
      // correct recovery path.
      setServerError(getErrorMessage(err, 'Incorrect code.') + ' Please log in again.')
      setTimeout(() => cancelMfaStep(), 1600)
    } finally {
      setMfaSubmitting(false)
    }
  }

  return (
    <div className="login-screen">
      <div className="login-card">
        <div className="login-card__header">
          <span className="login-wordmark">Ayiya<span className="login-wordmark__accent">POS</span></span>
          <p className="login-tagline">Sign in to your till</p>
        </div>

        {loginStage === 'password' && (
          <form className="login-form" onSubmit={passwordForm.handleSubmit(onSubmitPassword)} noValidate>
            <div className="login-field">
              <label htmlFor="email">Email</label>
              <input
                id="email"
                type="email"
                autoComplete="username"
                autoFocus
                {...passwordForm.register('email')}
              />
              {passwordForm.formState.errors.email && (
                <span className="login-field__error">{passwordForm.formState.errors.email.message}</span>
              )}
            </div>

            <div className="login-field">
              <label htmlFor="password">Password</label>
              <input
                id="password"
                type="password"
                autoComplete="current-password"
                {...passwordForm.register('password')}
              />
              {passwordForm.formState.errors.password && (
                <span className="login-field__error">{passwordForm.formState.errors.password.message}</span>
              )}
            </div>

            {serverError && <p className="login-error" role="alert">{serverError}</p>}

            <button type="submit" className="login-submit" disabled={passwordForm.formState.isSubmitting}>
              {passwordForm.formState.isSubmitting ? 'Signing in…' : 'Sign in'}
            </button>
          </form>
        )}

        {loginStage === 'awaiting-mfa' && (
          <div className="login-form">
            <p className="login-mfa-copy">
              Enter the 6-digit code from your authenticator app.
            </p>

            <OtpInput
              onComplete={handleOtpComplete}
              disabled={mfaSubmitting}
            />

            {serverError && <p className="login-error" role="alert">{serverError}</p>}
            {mfaSubmitting && !serverError && <p className="login-mfa-copy">Verifying…</p>}

            <button
              type="button"
              className="login-link-button"
              onClick={() => {
                cancelMfaStep()
                setServerError(null)
              }}
            >
              Back to sign in
            </button>
          </div>
        )}
      </div>
    </div>
  )
}