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
  { value: 'BankTransfer', label: 'Bank transfer' },
  { value: 'Card', label: 'Card' },
]

const BANKS = ['Absa Bank', 'Co-operative Bank']

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

  const updateMethod = (index: number, method: PaymentMethod) => {
    setLines((prev) => prev.map((line, i) => i === index ? {
      ...line,
      method,
      bankName: undefined,
      reference: undefined,
      isManualMpesa: false,
      manualMpesaVerified: false,
      bankTransferVerified: false,
    } : line))
  }

  const addLine = () => {
    setLines((prev) => [...prev, { method: 'Cash', amount: Math.max(0, remaining) }])
  }

  const removeLine = (index: number) => {
    setLines((prev) => prev.filter((_, i) => i !== index))
  }

  const canSubmit = !submitting && lines.length > 0 && Math.abs(remaining) < 0.01 && lines.every((line) =>
    line.amount > 0 &&
    (line.method !== 'BankTransfer' || Boolean(line.bankName && line.reference?.trim() && line.bankTransferVerified)) &&
    (line.method !== 'Mpesa' || !line.isManualMpesa || Boolean(line.bankName && line.reference?.trim() && line.manualMpesaVerified)),
  )

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
                  onChange={(e) => updateMethod(index, e.target.value as PaymentMethod)}
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

              {(line.method === 'Cash' || line.method === 'BankTransfer') && (
                <select
                  aria-label="Bank destination"
                  required={line.method === 'BankTransfer'}
                  value={line.bankName ?? ''}
                  disabled={submitting}
                  onChange={(e) => updateLine(index, { bankName: e.target.value || null, bankTransferVerified: false })}
                >
                  <option value="">{line.method === 'Cash' ? 'Cash drawer' : 'Choose receiving bank'}</option>
                  {BANKS.map((bank) => <option key={bank} value={bank}>{bank}</option>)}
                </select>
              )}

              {line.method === 'BankTransfer' && (
                <>
                  <input
                    required
                    placeholder="Bank transfer reference"
                    value={line.reference ?? ''}
                    disabled={submitting}
                    onChange={(e) => updateLine(index, { reference: e.target.value, bankTransferVerified: false })}
                  />
                  <label className="payment-line-confirmation">
                    <input type="checkbox" checked={Boolean(line.bankTransferVerified)} disabled={submitting} onChange={(e) => updateLine(index, { bankTransferVerified: e.target.checked })} />
                    I confirmed the transfer arrived
                  </label>
                </>
              )}

              {line.method === 'Mpesa' && (
                <>
                  <select
                    aria-label="M-Pesa payment type"
                    value={line.isManualMpesa ? 'manual' : 'stk'}
                    disabled={submitting}
                    onChange={(e) => updateLine(index, { isManualMpesa: e.target.value === 'manual', bankName: null, reference: null, manualMpesaVerified: false })}
                  >
                    <option value="stk">Send STK Push</option>
                    <option value="manual">Customer already paid (manual receipt)</option>
                  </select>
                  {line.isManualMpesa && (
                    <>
                      <select
                        aria-label="Bank used for M-Pesa payment"
                        required
                        value={line.bankName ?? ''}
                        disabled={submitting}
                        onChange={(e) => updateLine(index, { bankName: e.target.value || null, manualMpesaVerified: false })}
                      >
                        <option value="">Choose bank used</option>
                        {BANKS.map((bank) => <option key={bank} value={bank}>{bank}</option>)}
                      </select>
                      <input
                        required
                        placeholder="M-Pesa receipt code"
                        value={line.reference ?? ''}
                        disabled={submitting}
                        onChange={(e) => updateLine(index, { reference: e.target.value, manualMpesaVerified: false })}
                      />
                      <small className="payment-line-hint">Confirm the customer’s M-Pesa message before completing the sale.</small>
                      <label className="payment-line-confirmation">
                        <input
                          type="checkbox"
                          checked={Boolean(line.manualMpesaVerified)}
                          disabled={submitting}
                          onChange={(e) => updateLine(index, { manualMpesaVerified: e.target.checked })}
                        />
                        I confirmed the payment was received
                      </label>
                    </>
                  )}
                </>
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
