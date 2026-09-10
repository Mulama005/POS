import { useEffect, useRef, useState, type FormEvent } from 'react'
import { getPaymentStatus, initiateMpesaPayment, type MpesaPaymentStatus } from '../services/mpesaService'
import type { PaymentResult } from '../types/sale'
import { formatKes } from '../utils/currency'

interface MpesaWaitingModalProps {
  saleId: string
  payment: PaymentResult
  /** When more than one M-Pesa line is queued on this sale, e.g. "1 of 2" — omitted
   * (both undefined) when there's only a single M-Pesa payment on the sale. */
  queuePosition?: number
  queueTotal?: number
  /** Called once the cashier has acknowledged a final state (Success, Failed, or a
   * client-side timeout) — never called while still Pending. */
  onResolved: (finalStatus: MpesaPaymentStatus['status']) => void
}

const POLL_INTERVAL_MS = 3000
// STK Push prompts themselves typically expire around 60s server-side on Safaricom's
// end; this client-side countdown is a UX safety net so the cashier is never staring
// at a frozen screen, independent of whether Safaricom's own callback ever arrives.
const TIMEOUT_SECONDS = 90

type Stage = 'entering-phone' | 'waiting' | 'resolved'

function extractErrorMessage(err: unknown, fallback: string): string {
  const message = (err as { response?: { data?: { message?: string } } })?.response?.data?.message
  return message ?? fallback
}

function PhoneIcon() {
  return (
    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
      <path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6 19.79 19.79 0 0 1-3.07-8.67A2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.127.96.362 1.903.7 2.81a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.907.338 1.85.573 2.81.7A2 2 0 0 1 22 16.92z" />
    </svg>
  )
}

function CheckIcon() {
  return (
    <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <polyline points="20 6 9 17 4 12" />
    </svg>
  )
}

function XIcon() {
  return (
    <svg width="28" height="28" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
      <line x1="18" y1="6" x2="6" y2="18" /><line x1="6" y1="6" x2="18" y2="18" />
    </svg>
  )
}

/**
 * Step 27: triggers an STK Push and polls for the result. Known limitation, flagged
 * rather than hidden: if the payment ultimately fails or times out, there's currently
 * no backend endpoint to substitute a different payment method on a sale that's
 * already been completed — Sale.Total is validated against its Payments only once, at
 * completion time. For now, a failed M-Pesa attempt on a completed sale needs manual
 * follow-up (collect cash directly, reconcile later); it isn't silently converted to
 * anything here.
 */
export function MpesaWaitingModal({ saleId, payment, queuePosition, queueTotal, onResolved }: MpesaWaitingModalProps) {
  const [phoneNumber, setPhoneNumber] = useState('')
  const [stage, setStage] = useState<Stage>('entering-phone')
  const [status, setStatus] = useState<MpesaPaymentStatus['status']>('Pending')
  const [errorMessage, setErrorMessage] = useState<string | null>(null)
  const [secondsLeft, setSecondsLeft] = useState(TIMEOUT_SECONDS)
  const [submitting, setSubmitting] = useState(false)

  const pollTimer = useRef<ReturnType<typeof setInterval> | null>(null)
  const countdownTimer = useRef<ReturnType<typeof setInterval> | null>(null)

  const stopTimers = () => {
    if (pollTimer.current) clearInterval(pollTimer.current)
    if (countdownTimer.current) clearInterval(countdownTimer.current)
    pollTimer.current = null
    countdownTimer.current = null
  }

  useEffect(() => stopTimers, [])

  const startWaiting = async (phone: string) => {
    setErrorMessage(null)
    setSubmitting(true)
    try {
      await initiateMpesaPayment(saleId, payment.paymentId, phone)
      setStage('waiting')
      setStatus('Pending')
      setSecondsLeft(TIMEOUT_SECONDS)

      pollTimer.current = setInterval(() => {
        getPaymentStatus(saleId, payment.paymentId)
          .then((result) => {
            if (result.status !== 'Pending') {
              stopTimers()
              setStatus(result.status)
              setStage('resolved')
            }
          })
          .catch(() => {
            // A transient poll failure isn't fatal — the next tick tries again.
          })
      }, POLL_INTERVAL_MS)

      countdownTimer.current = setInterval(() => {
        setSecondsLeft((s) => {
          if (s <= 1) {
            stopTimers()
            setStatus('TimedOut')
            setStage('resolved')
            return 0
          }
          return s - 1
        })
      }, 1000)
    } catch (err) {
      setErrorMessage(extractErrorMessage(err, 'Could not start the M-Pesa payment. Try again.'))
    } finally {
      setSubmitting(false)
    }
  }

  function handleSubmitPhone(e: FormEvent) {
    e.preventDefault()
    void startWaiting(phoneNumber)
  }

  function handleRetry() {
    setStage('entering-phone')
    setStatus('Pending')
    setErrorMessage(null)
  }

  const showQueueBadge = queuePosition !== undefined && queueTotal !== undefined && queueTotal > 1
  const urgent = secondsLeft <= 15
  const progressPct = (secondsLeft / TIMEOUT_SECONDS) * 100

  return (
    <div className="mpesa-modal-backdrop" role="dialog" aria-modal="true">
      <div className="mpesa-modal">
        <div className="mpesa-modal-header">
          <div className="mpesa-modal-title-row">
            <span className="mpesa-modal-icon"><PhoneIcon /></span>
            <h2>M-Pesa payment</h2>
          </div>
          {showQueueBadge && (
            <span className="pos-badge pos-badge--neutral">Payment {queuePosition} of {queueTotal}</span>
          )}
        </div>
        <p className="mpesa-modal-amount">{formatKes(payment.amount)}</p>

        {stage === 'entering-phone' && (
          <form onSubmit={handleSubmitPhone}>
            <label className="mpesa-modal-label">
              Customer's phone number
              <input
                type="tel"
                placeholder="07xx xxx xxx"
                required
                autoFocus
                value={phoneNumber}
                disabled={submitting}
                onChange={(e) => setPhoneNumber(e.target.value)}
              />
            </label>
            {errorMessage && <div className="mpesa-modal-error" role="alert">{errorMessage}</div>}
            <div className="mpesa-modal-actions">
              <button type="submit" className="mpesa-modal-primary-btn" disabled={submitting || !phoneNumber}>
                {submitting ? 'Sending prompt…' : 'Send payment prompt'}
              </button>
            </div>
          </form>
        )}

        {stage === 'waiting' && (
          <div className="mpesa-waiting">
            <p className="mpesa-waiting-hint">
              A payment prompt was sent to <strong>{phoneNumber}</strong>. Ask the customer to enter
              their M-Pesa PIN to confirm.
            </p>
            <div className={`mpesa-progress-track ${urgent ? 'urgent' : ''}`}>
              <div className="mpesa-progress-fill" style={{ width: `${progressPct}%` }} />
            </div>
            <div className={`mpesa-countdown ${urgent ? 'urgent' : ''}`}>
              <span className="mpesa-pulse-dot" />
              Waiting… {secondsLeft}s
            </div>
          </div>
        )}

        {stage === 'resolved' && status === 'Success' && (
          <div className="mpesa-resolved">
            <div className="mpesa-resolved-icon success"><CheckIcon /></div>
            <p className="mpesa-resolved-hint">
              <span className="pos-badge pos-badge--success">Paid</span>
            </p>
            <div className="mpesa-modal-actions">
              <button type="button" className="mpesa-modal-primary-btn" onClick={() => onResolved('Success')}>
                Continue
              </button>
            </div>
          </div>
        )}

        {stage === 'resolved' && status !== 'Success' && (
          <div className="mpesa-resolved">
            <div className="mpesa-resolved-icon failed"><XIcon /></div>
            <p className="mpesa-resolved-hint">
              <span className="pos-badge pos-badge--danger">
                {status === 'TimedOut' ? 'Timed out' : 'Failed'}
              </span>
            </p>
            <p className="mpesa-resolved-detail">
              {status === 'TimedOut'
                ? 'The customer did not respond in time.'
                : 'The payment was not completed (declined or cancelled).'}
            </p>
            <p className="mpesa-modal-note">
              This sale is already recorded. If you don't retry successfully, collect payment manually
              and reconcile it outside the system for now — there's no automatic "switch payment
              method" yet.
            </p>
            <div className="mpesa-modal-actions mpesa-modal-actions--split">
              <button type="button" className="mpesa-modal-secondary-btn" onClick={handleRetry}>
                Retry
              </button>
              <button type="button" className="mpesa-modal-primary-btn" onClick={() => onResolved(status)}>
                Close
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}