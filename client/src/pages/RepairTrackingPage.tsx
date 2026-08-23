import { useState, type FormEvent } from 'react'
import { isAxiosError } from 'axios'
import { trackRepair } from '../services/repairsService'
import type { PublicRepairStatus } from '../types/phase6'
import './repairs.css'

export function RepairTrackingPage() {
  const [ticket, setTicket] = useState(''); const [phoneLast4, setPhoneLast4] = useState(''); const [result, setResult] = useState<PublicRepairStatus | null>(null); const [error, setError] = useState<string | null>(null); const [busy, setBusy] = useState(false)
  const submit = async (event: FormEvent) => { event.preventDefault(); setBusy(true); setError(null); setResult(null); try { setResult(await trackRepair(ticket.trim(), phoneLast4.trim())) } catch (e) { setError(isAxiosError(e) && e.response?.status === 404 ? 'We could not find a repair matching those details.' : 'Unable to check this repair right now.') } finally { setBusy(false) } }
  return <main className="service-page track-shell"><p className="service-eyebrow">AyiyaPOS repair care</p><h1 className="service-title">Track your repair</h1><p className="service-subtitle">Enter the ticket number on your receipt and the last four digits of your phone number.</p><section className="service-card" style={{ marginTop: 24 }}><form className="service-form" onSubmit={(e) => void submit(e)}><label>Repair ticket number<input required placeholder="RPR-20260822-001" value={ticket} onChange={e => setTicket(e.target.value)} /></label><label>Last 4 digits of phone number<input required inputMode="numeric" maxLength={4} value={phoneLast4} onChange={e => setPhoneLast4(e.target.value)} /></label><button className="service-button" disabled={busy}>{busy ? 'Checking…' : 'Check repair status'}</button></form>{error && <p className="service-alert" style={{ marginTop: 14 }}>{error}</p>}{result && <div className="track-result"><span className="status-pill">{result.status}</span><h2 style={{ marginTop: 12 }}>{result.deviceDescription}</h2><p className="service-subtitle">Ticket {result.ticketNumber}</p><p className="service-subtitle">Received {new Date(result.createdAt).toLocaleDateString()}</p></div>}</section></main>
}
