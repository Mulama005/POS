import { useState } from 'react'
import type { PaymentInput, PaymentMethod } from '../types/sale'
import { formatKes } from '../utils/currency'

interface PaymentModalProps {
  totalDue: number
  submitting: boolean
  errorMessage: string | null
  onCancel: () => void
  onSubmit: (payments: PaymentInput[]) => void
}

const METHODS: { value: PaymentMethod; label: string }[] = [
  { value: 'Cash', label: 'Cash' },
  { value: 'Mpesa', label: 'M-Pesa' },
  { value: 'Card', label: 'Card' },
]

/** Split-tender: the cashier can add more than one payment line (e.g. part cash, part
 * M-Pesa) as long as the total matches what's due. Mpesa/Card entries are recorded as
 * Pending here — Steps 27/28 wire up the real Daraja/Pesapal confirmation flow; for now
 * this just captures what was collected at the till. */
export function PaymentModal({ totalDue, submitting, errorMessage, onCancel, onSubmit }: PaymentModalProps) {
  const [lines, setLines] = useState<PaymentInput[]>([{ method: 'Cash', amount: totalDue }])

  const paidTotal = lines.reduce((sum, l) => sum + (Number.isFinite(l.amount) ? l.amount : 0), 0)
  const remaining = Math.round((totalDue - paidTotal) * 100) / 100

  const updateLine = (index: number, patch: Partial<PaymentInput>) => {
    setLines((prev) => prev.map((l, i) => (i === index ? { ...l, ...patch } : l)))
  }

  const addLine = () => {
    setLines((prev) => [...prev, { method: 'Cash', amount: Math.max(0, remaining) }])
  }

  const removeLine = (index: number) => {
    setLines((prev) => prev.filter((_, i) => i !== index))
  }

  const canSubmit = !submitting && lines.length > 0 && Math.abs(remaining) < 0.01 && lines.every((l) => l.amount > 0)

  return (
    <div className="checkout-modal-backdrop" role="dialog" aria-modal="true">
      <div className="checkout-modal">
        <h2>Take payment</h2>
        <p className="checkout-modal__subtitle">Total due: {formatKes(totalDue)}</p>

        <div className="payment-lines">
          {lines.map((line, index) => (
            <div className="payment-line-group" key={index}>
              <div className="payment-line">
                <select
                  value={line.method}
                  disabled={submitting}
                  onChange={(e) => updateLine(index, { method: e.target.value as PaymentMethod })}
                >
                  {METHODS.map((m) => (
                    <option key={m.value} value={m.value}>
                      {m.label}
                    </option>
                  ))}
                </select>

                <input
                  type="number"
                  min={0}
                  step="0.01"
                  value={line.amount || ''}
                  disabled={submitting}
                  onChange={(e) => updateLine(index, { amount: Math.max(0, Number(e.target.value) || 0) })}
                />

                {lines.length > 1 && (
                  <button type="button" disabled={submitting} onClick={() => removeLine(index)} aria-label="Remove payment line">
                    ×
                  </button>
                )}
              </div>

              {line.method === 'Mpesa' && (
                <input
                  type="tel"
                  placeholder="M-Pesa phone number (07xx…)"
                  className="payment-line-phone"
                  value={line.mpesaPhoneNumber ?? ''}
                  disabled={submitting}
                  onChange={(e) => updateLine(index, { mpesaPhoneNumber: e.target.value })}
                />
              )}
            </div>
          ))}
        </div>

        <button type="button" className="payment-add-line" disabled={submitting} onClick={addLine}>
          + Split payment
        </button>

        <div className={`payment-remaining ${Math.abs(remaining) < 0.01 ? 'payment-remaining--ok' : ''}`}>
          {remaining > 0.005 && <>Remaining: {formatKes(remaining)}</>}
          {remaining < -0.005 && <>Overpaid by {formatKes(-remaining)}</>}
          {Math.abs(remaining) < 0.01 && <>Fully covered</>}
        </div>

        {errorMessage && <div className="checkout-modal__error">{errorMessage}</div>}

        <div className="checkout-modal__actions">
          <button type="button" disabled={submitting} onClick={onCancel}>
            Cancel
          </button>
          <button type="button" className="checkout-complete-btn" disabled={!canSubmit} onClick={() => onSubmit(lines)}>
            {submitting ? 'Completing sale…' : 'Complete sale'}
          </button>
        </div>
      </div>
    </div>
  )
}