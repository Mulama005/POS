import { useState, type FormEvent } from 'react'
import { formatKes } from '../utils/currency'

interface TillOpenModalProps {
  registerName: string
  submitting: boolean
  errorMessage: string | null
  onCancel: () => void
  onOpen: (openingFloat: number) => void
}

export function TillOpenModal({ registerName, submitting, errorMessage, onCancel, onOpen }: TillOpenModalProps) {
  const [openingFloat, setOpeningFloat] = useState('')
  const amount = Number(openingFloat)
  const canSubmit = !submitting && openingFloat.trim() !== '' && amount >= 0

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault()
    if (canSubmit) onOpen(amount)
  }

  return (
    <div className="checkout-modal-backdrop" role="dialog" aria-modal="true">
      <div className="checkout-modal">
        <h2>Open till</h2>
        <p className="checkout-modal__subtitle">{registerName}</p>
        <p className="checkout-modal__hint">
          Count the cash currently in the drawer and enter it below before starting the shift.
        </p>

        <form onSubmit={handleSubmit}>
          <label>
            Opening float (KES)
            <input
              type="number"
              min={0}
              step="0.01"
              autoFocus
              required
              value={openingFloat}
              disabled={submitting}
              onChange={(e) => setOpeningFloat(e.target.value)}
            />
          </label>

          {openingFloat.trim() !== '' && amount >= 0 && (
            <p className="checkout-modal__hint">Opening with {formatKes(amount)}.</p>
          )}

          {errorMessage && <div className="checkout-modal__error">{errorMessage}</div>}

          <div className="checkout-modal__actions">
            <button type="button" disabled={submitting} onClick={onCancel}>
              Cancel
            </button>
            <button type="submit" className="checkout-complete-btn" disabled={!canSubmit}>
              {submitting ? 'Opening…' : 'Open till'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}