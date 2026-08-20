import { useState, type FormEvent } from 'react'

interface TillCloseModalProps {
  registerName: string
  submitting: boolean
  errorMessage: string | null
  onCancel: () => void
  onClose: (countedCashAtClose: number) => void
}

export function TillCloseModal({ registerName, submitting, errorMessage, onCancel, onClose }: TillCloseModalProps) {
  const [countedCash, setCountedCash] = useState('')
  const amount = Number(countedCash)
  const canSubmit = !submitting && countedCash.trim() !== '' && amount >= 0

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    if (canSubmit) onClose(amount)
  }

  return (
    <div className="checkout-modal-backdrop" role="dialog" aria-modal="true">
      <div className="checkout-modal">
        <h2>Close till</h2>
        <p className="checkout-modal__subtitle">{registerName}</p>
        <p className="checkout-modal__hint">
          Count all cash currently in the drawer (including the opening float) and enter the total below.
        </p>

        <form onSubmit={handleSubmit}>
          <label>
            Counted cash (KES)
            <input
              type="number"
              min={0}
              step="0.01"
              autoFocus
              required
              value={countedCash}
              disabled={submitting}
              onChange={(e) => setCountedCash(e.target.value)}
            />
          </label>

          {errorMessage && <div className="checkout-modal__error">{errorMessage}</div>}

          <div className="checkout-modal__actions">
            <button type="button" disabled={submitting} onClick={onCancel}>
              Cancel
            </button>
            <button type="submit" className="checkout-complete-btn" disabled={!canSubmit}>
              {submitting ? 'Closing…' : 'Close till'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}