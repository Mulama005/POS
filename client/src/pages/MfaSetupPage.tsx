import { useEffect, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { QRCodeSVG } from 'qrcode.react'
import { isAxiosError } from 'axios'
import { useAuth } from '../hooks/useAuth'
import { mfaEnable, mfaSetup } from '../services/authService'
import { getLandingRouteForRole } from '../utils/roleRedirect'
import { OtpInput } from '../components/OtpInput'
import type { ApiErrorBody } from '../types/auth'
import './MfaSetupPage.css'

function getErrorMessage(err: unknown, fallback: string): string {
  if (isAxiosError<ApiErrorBody>(err) && err.response?.data?.message) {
    return err.response.data.message
  }
  return fallback
}

export function MfaSetupPage() {
  const { user, refreshSession } = useAuth()
  const navigate = useNavigate()

  const [otpAuthUri, setOtpAuthUri] = useState<string | null>(null)
  const [rawSecret, setRawSecret] = useState<string | null>(null)
  const [loadError, setLoadError] = useState<string | null>(null)
  const [confirmError, setConfirmError] = useState<string | null>(null)
  const [confirming, setConfirming] = useState(false)
  const [copied, setCopied] = useState(false)

  useEffect(() => {
    // /mfa/setup can safely be called again if this page reloads — the backend
    // generates a fresh secret each time and MfaEnabled stays false until a
    // real code confirms it, so there's no risk of a stale/half-enrolled state.
    mfaSetup()
      .then((res) => {
        setOtpAuthUri(res.otpAuthUri)
        setRawSecret(res.rawSecret)
      })
      .catch((err) => setLoadError(getErrorMessage(err, 'Could not start MFA setup.')))
  }, [])

  const handleCopySecret = async () => {
    if (!rawSecret) return
    await navigator.clipboard.writeText(rawSecret)
    setCopied(true)
    setTimeout(() => setCopied(false), 1500)
  }

  const handleConfirm = async (code: string) => {
    setConfirmError(null)
    setConfirming(true)
    try {
      await mfaEnable(code)
      await refreshSession() // picks up mfaEnabled: true from the backend fix
      if (user) navigate(getLandingRouteForRole(user.role), { replace: true })
    } catch (err) {
      setConfirmError(getErrorMessage(err, 'Incorrect code. Try again.'))
    } finally {
      setConfirming(false)
    }
  }

  return (
    <div className="mfa-setup-screen">
      <div className="mfa-setup-card">
        <h1 className="mfa-setup-title">Set up two-factor authentication</h1>
        <p className="mfa-setup-subtitle">
          This adds a second step to sign-in using an authenticator app — recommended
          for Manager and Admin accounts.
        </p>

        {loadError && <p className="mfa-setup-error" role="alert">{loadError}</p>}

        {otpAuthUri && rawSecret && (
          <>
            <ol className="mfa-setup-steps">
              <li>
                Open an authenticator app (Google Authenticator, Authy, Microsoft
                Authenticator, etc.) and scan this code:
                <div className="mfa-setup-qr">
                  <QRCodeSVG value={otpAuthUri} size={176} />
                </div>
              </li>
              <li>
                Can't scan it? Enter this key manually instead:
                <div className="mfa-setup-secret">
                  <code>{rawSecret}</code>
                  <button type="button" className="mfa-setup-copy-btn" onClick={() => void handleCopySecret()}>
                    {copied ? 'Copied' : 'Copy'}
                  </button>
                </div>
              </li>
              <li>Enter the 6-digit code the app is now showing:</li>
            </ol>

            <OtpInput onComplete={(code) => void handleConfirm(code)} disabled={confirming} />

            {confirmError && <p className="mfa-setup-error" role="alert">{confirmError}</p>}
            {confirming && !confirmError && <p className="mfa-setup-hint">Verifying…</p>}
          </>
        )}

        <button
          type="button"
          className="mfa-setup-skip-btn"
          onClick={() => {
            if (user) navigate(getLandingRouteForRole(user.role))
          }}
        >
          Skip for now
        </button>
      </div>
    </div>
  )
}