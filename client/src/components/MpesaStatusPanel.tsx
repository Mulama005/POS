import { useEffect, useRef, useState, type FormEvent } from 'react'
import { getPaymentStatus, initiateMpesaPayment, type MpesaPaymentStatus } from '../services/paymentsService'
import type { PaymentResult } from '../types/sale'
import { formatKes } from '../utils/currency'

interface MpesaWaitingModalProps {
  saleId: string
  payment: PaymentResult
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

/**
 * Step 27: triggers an STK Push and polls for the result. Known limitation, flagged
 * rather than hidden: if the payment ultimately fails or times out, there's currently
 * no backend endpoint to substitute a different payment method on a sale that's
 * already been completed — Sale.Total is validated against its Payments only once, at
 * completion time. For now, a failed M-Pesa attempt on a completed sale needs manual
 * follow-up (collect cash directly, reconcile later); it isn't silently converted to
 * anything here.
 */
export function MpesaWaitingModal({ saleId, payment, onResolved }: MpesaWaitingModalProps) {
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

  return (
    <div className="checkout-modal-backdrop" role="dialog" aria-modal="true">
      <div className="checkout-modal">
        <h2>M-Pesa payment</h2>
        <p className="checkout-modal__subtitle">Amount due: {formatKes(payment.amount)}</p>

        {stage === 'entering-phone' && (
          <form onSubmit={handleSubmitPhone}>
            <label>
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
            {errorMessage && <div className="checkout-modal__error">{errorMessage}</div>}
            <div className="checkout-modal__actions">
              <button type="submit" className="checkout-complete-btn" disabled={submitting || !phoneNumber}>
                {submitting ? 'Sending prompt…' : 'Send payment prompt'}
              </button>
            </div>
          </form>
        )}

        {stage === 'waiting' && (
          <div className="mpesa-waiting">
            <p className="mpesa-waiting__hint">
              A payment prompt was sent to {phoneNumber}. Ask the customer to enter their M-Pesa PIN to confirm.
            </p>
            <div className="mpesa-waiting__countdown">Waiting… {secondsLeft}s</div>
          </div>
        )}

        {stage === 'resolved' && status === 'Success' && (
          <div className="mpesa-waiting">
            <p className="mpesa-waiting__hint mpesa-waiting__hint--success">Payment received.</p>
            <div className="checkout-modal__actions">
              <button type="button" className="checkout-complete-btn" onClick={() => onResolved('Success')}>
                Continue
              </button>
            </div>
          </div>
        )}

        {stage === 'resolved' && status !== 'Success' && (
          <div className="mpesa-waiting">
            <p className="mpesa-waiting__hint mpesa-waiting__hint--error">
              {status === 'TimedOut'
                ? 'The customer did not respond in time.'
                : 'The payment was not completed (declined or cancelled).'}
            </p>
            <p className="mpesa-waiting__note">
              This sale is already recorded. If you don't retry successfully, collect payment manually and
              reconcile it outside the system for now — there's no automatic "switch payment method" yet.
            </p>
            <div className="checkout-modal__actions">
              <button type="button" onClick={handleRetry}>
                Retry
              </button>
              <button type="button" className="checkout-complete-btn" onClick={() => onResolved(status)}>
                Close
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}