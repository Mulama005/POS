import { useState, type FormEvent } from 'react'
import { isAxiosError } from 'axios'
import { warrantyLookup, type WarrantyInfo } from '../services/stockService'
import type { ApiErrorBody } from '../types/auth'
import './WarrantyLookupPage.css'

function getErrorMessage(err: unknown, fallback: string): string {
  if (isAxiosError<ApiErrorBody>(err) && err.response?.status === 404) {
    return 'No unit found with that serial number or IMEI.'
  }
  if (isAxiosError<ApiErrorBody>(err) && err.response?.data?.message) {
    return err.response.data.message
  }
  return fallback
}

function formatDate(value: string | null): string {
  if (!value) return '—'
  return new Date(value).toLocaleDateString('en-KE', { day: 'numeric', month: 'short', year: 'numeric' })
}

export function WarrantyLookupPage() {
  const [serial, setSerial] = useState('')
  const [info, setInfo] = useState<WarrantyInfo | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault()
    const trimmed = serial.trim()
    if (!trimmed) return
    setLoading(true)
    setError(null)
    setInfo(null)
    try {
      setInfo(await warrantyLookup(trimmed))
    } catch (err) {
      setError(getErrorMessage(err, 'Could not complete the lookup.'))
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="warranty-screen">
      <h1 className="warranty-title">Warranty lookup</h1>

      <form className="warranty-search-row" onSubmit={(e) => void handleSubmit(e)}>
        <input
          type="text"
          placeholder="Serial number or IMEI"
          value={serial}
          onChange={(e) => setSerial(e.target.value)}
          autoFocus
        />
        <button type="submit" disabled={loading || !serial.trim()}>
          {loading ? 'Checking…' : 'Check'}
        </button>
      </form>

      {error && <p className="warranty-error" role="alert">{error}</p>}

      {info && (
        <div className="warranty-result">
          <div className="warranty-result-header">
            <div>
              <div className="warranty-result-name">{info.name}</div>
              <div className="warranty-result-serial">{info.serial}</div>
            </div>
            <span className={`pos-badge ${info.isUnderWarranty ? 'pos-badge--success' : 'pos-badge--danger'}`}>
              {info.isUnderWarranty ? 'Under warranty' : 'Expired'}
            </span>
          </div>

          <div className="pos-ledger-row">
            <span className="pos-ledger-label">Unit status</span>
            <span className="pos-leader" />
            <span className="pos-ledger-value">{info.status}</span>
          </div>
          <div className="pos-ledger-row">
            <span className="pos-ledger-label">Sale date</span>
            <span className="pos-leader" />
            <span className="pos-ledger-value">{formatDate(info.saleDate)}</span>
          </div>
          <div className="pos-ledger-row">
            <span className="pos-ledger-label">Warranty period</span>
            <span className="pos-leader" />
            <span className="pos-ledger-value">{info.warrantyMonths} months</span>
          </div>
          <div className="pos-ledger-row">
            <span className="pos-ledger-label">Expiry date</span>
            <span className="pos-leader" />
            <span className="pos-ledger-value">{formatDate(info.expiryDate)}</span>
          </div>
        </div>
      )}
    </div>
  )
}