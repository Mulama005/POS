import { useEffect, useState } from 'react'
import { isAxiosError } from 'axios'
import { apiClient } from '../services/apiClient'
import { formatKes } from '../utils/currency'
import type { ApiErrorBody } from '../types/auth'
import './ManagerDashboard.css'

interface Summary {
  todaySales: number
  totalOrders: number
  avgOrderValue: number
  activeRegisters: number
  totalRegisters: number
}

interface RegisterStatus {
  id: string
  name: string
  cashier: string
  status: 'Open' | 'Closed'
  expected: number
  counted: number
}

interface StockAlert {
  id: string
  name: string
  sku: string
  current: number
  threshold: number
}

interface PendingApproval {
  id: string
  transactionId: string
  amount: number
  reason: string
  time: string
  type: 'Refund' | 'Void'
}

function getErrorMessage(err: unknown, fallback: string): string {
  if (isAxiosError<ApiErrorBody>(err) && err.response?.data?.message) {
    return err.response.data.message
  }
  return fallback
}

/** The dotted-leader row: label ... value. See .pos-ledger-row in index.css. */
function LedgerRow({ label, value, tone }: { label: string; value: string; tone?: 'success' | 'warn' | 'danger' }) {
  return (
    <div className="pos-ledger-row">
      <span className="pos-ledger-label">{label}</span>
      <span className="pos-leader" />
      <span className={`pos-ledger-value ${tone ? `tone-${tone}` : ''}`}>{value}</span>
    </div>
  )
}

function RegisterEntry({ reg }: { reg: RegisterStatus }) {
  const isOpen = reg.status === 'Open'
  const diff = reg.counted - reg.expected
  const isBalanced = Math.abs(diff) < 0.01

  return (
    <div className="mgr-register-entry">
      <LedgerRow label={reg.name} value={reg.status} tone={isOpen ? 'success' : undefined} />
      {isOpen && (
        <div className="mgr-register-sub">
          <span>{reg.cashier}</span>
          <span className={isBalanced ? 'tone-success' : Math.abs(diff) > reg.expected * 0.02 ? 'tone-danger' : 'tone-warn'}>
            {isBalanced ? 'Balanced' : `${diff > 0 ? '+' : ''}${formatKes(diff)}`}
          </span>
        </div>
      )}
    </div>
  )
}

export function ManagerDashboard() {
  const [summary, setSummary] = useState<Summary | null>(null)
  const [registers, setRegisters] = useState<RegisterStatus[]>([])
  const [stockAlerts, setStockAlerts] = useState<StockAlert[]>([])
  const [approvals, setApprovals] = useState<PendingApproval[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [settled, setSettled] = useState<Record<string, 'approved' | 'rejected'>>({})

  const fetchData = async () => {
    setError(null)
    try {
      const [summaryRes, registersRes, stockRes, approvalsRes] = await Promise.all([
        apiClient.get<Summary>('/api/manager/dashboard/summary'),
        apiClient.get<RegisterStatus[]>('/api/manager/dashboard/registers'),
        apiClient.get<StockAlert[]>('/api/manager/dashboard/stock-alerts'),
        apiClient.get<PendingApproval[]>('/api/manager/dashboard/pending-approvals'),
      ])
      setSummary(summaryRes.data)
      setRegisters(registersRes.data)
      setStockAlerts(stockRes.data)
      setApprovals(approvalsRes.data)
    } catch (err) {
      setError(getErrorMessage(err, 'Could not load the dashboard.'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void fetchData()
    const interval = setInterval(() => void fetchData(), 30000)
    return () => clearInterval(interval)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  const settle = async (id: string, action: 'approved' | 'rejected') => {
    setSettled((s) => ({ ...s, [id]: action }))
    try {
      const endpoint = action === 'approved' ? 'approve' : 'reject'
      await apiClient.post(`/api/manager/dashboard/approvals/${id}/${endpoint}`)
      setTimeout(() => {
        setApprovals((a) => a.filter((p) => p.id !== id))
        setSettled((s) => {
          const next = { ...s }
          delete next[id]
          return next
        })
      }, 600)
    } catch {
      setSettled((s) => {
        const next = { ...s }
        delete next[id]
        return next
      })
    }
  }

  if (loading) {
    return <div className="mgr-loading">Loading dashboard…</div>
  }

  if (error || !summary) {
    return (
      <div className="mgr-error-screen">
        <p>{error ?? 'No data available.'}</p>
        <button type="button" onClick={() => void fetchData()}>Retry</button>
      </div>
    )
  }

  return (
    <div className="manager-dashboard">
      <div className="mgr-header">
        <h1>Manager dashboard</h1>
        <span className="mgr-date">
          {new Date().toLocaleDateString('en-KE', { weekday: 'long', day: 'numeric', month: 'short', year: 'numeric' })}
        </span>
      </div>

      <div className="mgr-hero">
        <span className="mgr-hero-label">Today's takings</span>
        <span className="mgr-hero-value">{formatKes(summary.todaySales)}</span>
        <div className="mgr-hero-stats">
          <span><strong>{summary.totalOrders}</strong> orders</span>
          <span className="sep">·</span>
          <span>avg <strong>{formatKes(summary.avgOrderValue)}</strong></span>
          <span className="sep">·</span>
          <span><strong>{summary.activeRegisters}/{summary.totalRegisters}</strong> registers open</span>
        </div>
      </div>

      <div className="mgr-columns">
        <section className="mgr-section">
          <div className="mgr-section-header">
            <h2>Register status</h2>
            <span className="mgr-section-count">{summary.activeRegisters} open</span>
          </div>
          {registers.length === 0 ? (
            <p className="mgr-empty">No registers configured yet.</p>
          ) : (
            registers.map((reg) => <RegisterEntry key={reg.id} reg={reg} />)
          )}
        </section>

        <section className="mgr-section">
          <div className="mgr-section-header">
            <h2>Low stock</h2>
            {stockAlerts.length > 0 && (
              <span className="mgr-section-count tone-danger">
                {stockAlerts.filter((s) => s.current === 0).length} out
              </span>
            )}
          </div>
          {stockAlerts.length === 0 ? (
            <p className="mgr-empty">Nothing below its reorder threshold.</p>
          ) : (
            stockAlerts.map((item) => (
              <LedgerRow
                key={item.id}
                label={item.name}
                value={`${item.current} / ${item.threshold}`}
                tone={item.current === 0 ? 'danger' : 'warn'}
              />
            ))
          )}
        </section>
      </div>

      <section className="mgr-section mgr-section--full">
        <div className="mgr-section-header">
          <h2>Pending approvals</h2>
          {approvals.length > 0 && <span className="mgr-section-count tone-danger">{approvals.length} pending</span>}
        </div>

        {approvals.length === 0 ? (
          <p className="mgr-empty">No refund or void approvals waiting on you right now.</p>
        ) : (
          <table className="mgr-approvals-table">
            <thead>
              <tr>
                <th>Transaction</th><th>Type</th><th>Amount</th><th>Reason</th><th>Time</th><th></th>
              </tr>
            </thead>
            <tbody>
              {approvals.map((p) => {
                const state = settled[p.id]
                return (
                  <tr key={p.id} style={{ opacity: state ? 0 : 1, transition: 'opacity 400ms ease' }}>
                    <td className="mgr-mono">{p.transactionId}</td>
                    <td>{p.type}</td>
                    <td className="mgr-mono">{formatKes(p.amount)}</td>
                    <td>{p.reason}</td>
                    <td className="mgr-mono">{p.time}</td>
                    <td>
                      <div className="mgr-approval-actions">
                        <button type="button" onClick={() => void settle(p.id, 'approved')} disabled={!!state}>
                          Approve
                        </button>
                        <button type="button" className="mgr-reject-btn" onClick={() => void settle(p.id, 'rejected')} disabled={!!state}>
                          Reject
                        </button>
                      </div>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        )}
      </section>
    </div>
  )
}