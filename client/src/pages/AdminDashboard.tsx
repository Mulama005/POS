import { useEffect, useState } from 'react'
import { isAxiosError } from 'axios'
import { apiClient } from '../services/apiClient'
import type { ApiErrorBody } from '../types/auth'
import './AdminDashboard.css'

type StatusLevel = 'ok' | 'warn' | 'error'

interface ServiceStatus {
  name: string
  label: string
  status: StatusLevel
  detail: string
  meta: string
  latency?: string
}

interface AuditEntry {
  id: number
  ts: string
  user: string
  action: string
  details: string
  level: 'info' | 'warn' | 'error'
}

function getErrorMessage(err: unknown, fallback: string): string {
  if (isAxiosError<ApiErrorBody>(err) && err.response?.data?.message) {
    return err.response.data.message
  }
  return fallback
}

const STATUS_TONE: Record<StatusLevel, string> = { ok: 'tone-success', warn: 'tone-warn', error: 'tone-danger' }

function ServiceEntry({ svc }: { svc: ServiceStatus }) {
  return (
    <div className="pos-ledger-row">
      <span className="pos-ledger-label">{svc.label}</span>
      <span className="pos-leader" />
      <span className={`pos-ledger-value ${STATUS_TONE[svc.status]}`}>
        {svc.detail}{svc.latency ? ` · ${svc.latency}` : ''}
      </span>
    </div>
  )
}

export function AdminDashboard() {
  const [services, setServices] = useState<ServiceStatus[]>([])
  const [auditLog, setAuditLog] = useState<AuditEntry[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const fetchData = async () => {
    setError(null)
    try {
      const [healthRes, auditRes] = await Promise.all([
        apiClient.get<ServiceStatus[]>('/api/admin/health'),
        apiClient.get<AuditEntry[]>('/api/admin/audit', { params: { limit: 20 } }),
      ])
      setServices(healthRes.data)
      setAuditLog(auditRes.data)
    } catch (err) {
      setError(getErrorMessage(err, 'Could not load system status.'))
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

  const okCount = services.filter((s) => s.status === 'ok').length
  const warnCount = services.filter((s) => s.status === 'warn').length
  const errCount = services.filter((s) => s.status === 'error').length

  if (loading) {
    return <div className="admin-loading">Loading system status…</div>
  }

  if (error) {
    return (
      <div className="admin-error-screen">
        <p>{error}</p>
        <button type="button" onClick={() => void fetchData()}>Retry</button>
      </div>
    )
  }

  return (
    <div className="admin-dashboard">
      <div className="admin-header">
        <h1>Admin dashboard</h1>
        <span className="admin-timestamp">{new Date().toLocaleTimeString('en-KE', { hour12: false })}</span>
      </div>

      <div className="admin-summary-line">
        <span className="tone-success">{okCount} OK</span>
        <span className="sep">·</span>
        <span className="tone-warn">{warnCount} WARN</span>
        <span className="sep">·</span>
        <span className="tone-danger">{errCount} ERR</span>
        <span className="sep">·</span>
        <span>{services.length} integrations monitored</span>
      </div>

      <section className="admin-section">
        <div className="admin-section-header">
          <h2>Integrations</h2>
        </div>
        {services.map((svc) => <ServiceEntry key={svc.name} svc={svc} />)}
      </section>

      <section className="admin-section admin-section--log">
        <div className="admin-section-header">
          <h2>Audit log</h2>
          <span className="admin-section-count">{auditLog.length} recent entries</span>
        </div>
        {auditLog.length === 0 ? (
          <p className="admin-empty">No audit entries recorded yet.</p>
        ) : (
          <div className="admin-log">
            {auditLog.map((row) => (
              <div key={row.id} className="admin-log-row">
                <span className="admin-log-ts">{row.ts}</span>
                <span className="admin-log-user" title={row.user}>{row.user}</span>
                <span className="admin-log-action">{row.action}</span>
                <span className="admin-log-details">{row.details}</span>
                <span className={`admin-log-level ${STATUS_TONE[row.level === 'error' ? 'error' : row.level === 'warn' ? 'warn' : 'ok']}`}>
                  {row.level.toUpperCase()}
                </span>
              </div>
            ))}
          </div>
        )}
      </section>
    </div>
  )
}