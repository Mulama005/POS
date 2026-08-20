import { useState, type FormEvent } from 'react'
import { formatKes } from '../utils/currency'

interface DiscountApprovalModalProps {
  discountAmount: number
  message: string
  submitting: boolean
  errorMessage: string | null
  onCancel: () => void
  onApprove: (email: string, password: string) => void
}

/** A Manager/Admin types their own credentials here to approve a discount over the
 * threshold — this never touches the cashier's active session; it just proves "a
 * Manager/Admin authorized this specific discount" (see approve-discount endpoint /
 * IDiscountApprovalStore on the backend). */
export function DiscountApprovalModal({
  discountAmount,
  message,
  submitting,
  errorMessage,
  onCancel,
  onApprove,
}: DiscountApprovalModalProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    onApprove(email, password)
  }

  return (
    <div className="checkout-modal-backdrop" role="dialog" aria-modal="true">
      <div className="checkout-modal">
        <h2>Manager approval needed</h2>
        <p className="checkout-modal__subtitle">
          This sale's discount ({formatKes(discountAmount)}) needs a Manager or Admin to approve it.
        </p>
        <p className="checkout-modal__hint">{message}</p>

        <form onSubmit={handleSubmit}>
          <label>
            Manager / Admin email
            <input
              type="email"
              required
              autoFocus
              value={email}
              disabled={submitting}
              onChange={(e) => setEmail(e.target.value)}
            />
          </label>
          <label>
            Password
            <input
              type="password"
              required
              value={password}
              disabled={submitting}
              onChange={(e) => setPassword(e.target.value)}
            />
          </label>

          {errorMessage && <div className="checkout-modal__error">{errorMessage}</div>}

          <div className="checkout-modal__actions">
            <button type="button" disabled={submitting} onClick={onCancel}>
              Cancel
            </button>
            <button type="submit" disabled={submitting || !email || !password}>
              {submitting ? 'Checking…' : 'Approve discount'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}