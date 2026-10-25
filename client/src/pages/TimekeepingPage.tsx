import { useCallback, useEffect, useState } from 'react'
import { isAxiosError } from 'axios'
import LoadingScreen from '../components/LoadingScreen'
import { useAuth } from '../hooks/useAuth'
import { apiClient } from '../services/apiClient'
import { downloadBlob } from '../utils/downloadFile'
import './TimekeepingPage.css'

interface Technician { id: string; fullName: string }
interface TimeEntry {
  id: string
  technicianId: string
  technicianName: string
  clockInAt: string
  clockInRecordedBy: string
  clockOutAt: string | null
  clockOutRecordedBy: string | null
  isOpen: boolean
  hoursWorked: number
}
interface TimekeepingData { technicians: Technician[]; entries: TimeEntry[]; totalHours: number; openShifts: number }

const nairobiToday = () => {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Africa/Nairobi', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date())
  return `${parts.find(x => x.type === 'year')?.value}-${parts.find(x => x.type === 'month')?.value}-${parts.find(x => x.type === 'day')?.value}`
}
const thirtyDaysAgo = (day: string) => {
  const date = new Date(`${day}T00:00:00Z`)
  date.setUTCDate(date.getUTCDate() - 30)
  return date.toISOString().slice(0, 10)
}
const formatDateTime = (value: string | null) => value
  ? new Date(value).toLocaleString('en-KE', { timeZone: 'Africa/Nairobi', dateStyle: 'medium', timeStyle: 'short' })
  : 'Shift in progress'
const errorMessage = (error: unknown) => isAxiosError(error)
  ? (typeof error.response?.data === 'string' ? error.response.data : error.response?.data?.message ?? 'The request could not be completed.')
  : 'The request could not be completed.'

export default function TimekeepingPage() {
  const { accessToken } = useAuth()
  const [toDate, setToDate] = useState(nairobiToday)
  const [fromDate, setFromDate] = useState(() => thirtyDaysAgo(nairobiToday()))
  const [data, setData] = useState<TimekeepingData | null>(null)
  const [selectedTechnicianId, setSelectedTechnicianId] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')

  const load = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const response = await apiClient.get<TimekeepingData>('/api/timekeeping', { params: { fromDate, toDate } })
      setData(response.data)
      const openIds = new Set(response.data.entries.filter(entry => entry.isOpen).map(entry => entry.technicianId))
      setSelectedTechnicianId(current => response.data.technicians.some(technician => technician.id === current && !openIds.has(current))
        ? current
        : response.data.technicians.find(technician => !openIds.has(technician.id))?.id ?? '')
    } catch (requestError) {
      setError(errorMessage(requestError))
    } finally {
      setLoading(false)
    }
  }, [fromDate, toDate])

  useEffect(() => { void load() }, [accessToken, load])

  const clockIn = async () => {
    if (!selectedTechnicianId) return
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await apiClient.post('/api/timekeeping/clock-in', { technicianId: selectedTechnicianId })
      setNotice('Technician clocked in and recorded.')
      await load()
    } catch (requestError) {
      setError(errorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  const clockOut = async (entry: TimeEntry) => {
    setBusy(true)
    setError('')
    setNotice('')
    try {
      await apiClient.post(`/api/timekeeping/${entry.id}/clock-out`)
      setNotice(`${entry.technicianName} clocked out and recorded.`)
      await load()
    } catch (requestError) {
      setError(errorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  const exportPdf = async () => {
    setBusy(true)
    setError('')
    try {
      const { data: file } = await apiClient.get<Blob>('/api/timekeeping/export/pdf', { params: { fromDate, toDate }, responseType: 'blob' })
      downloadBlob(file, `technician-timekeeping-${fromDate}-${toDate}.pdf`)
      setNotice('Your printable timekeeping summary is downloading.')
    } catch (requestError) {
      setError(errorMessage(requestError))
    } finally {
      setBusy(false)
    }
  }

  if (loading && !data) return <LoadingScreen message="Loading technician timekeeping…" />

  const openTechnicianIds = new Set(data?.entries.filter(entry => entry.isOpen).map(entry => entry.technicianId) ?? [])
  const availableTechnicians = data?.technicians.filter(technician => !openTechnicianIds.has(technician.id)) ?? []

  return <main className="timekeeping-page">
    <header className="timekeeping-header">
      <div><p className="timekeeping-eyebrow">Staff monitoring · EAT (UTC+3)</p><h1>Timekeeping</h1><p>Managers and Admins record technician clock-ins and clock-outs here.</p></div>
      <button className="timekeeping-export" type="button" disabled={busy} onClick={() => void exportPdf()}>Print PDF summary</button>
    </header>

    {error && <div className="timekeeping-message timekeeping-message--error" role="alert">{error}</div>}
    {notice && <div className="timekeeping-message" role="status">{notice}</div>}

    <section className="timekeeping-clock-panel">
      <div><p className="timekeeping-eyebrow">New clock-in</p><h2>Start a technician shift</h2><p>The time is captured from the system when you record it.</p></div>
      <div className="timekeeping-clock-controls">
        <label>Technician<select value={selectedTechnicianId} disabled={busy || availableTechnicians.length === 0} onChange={event => setSelectedTechnicianId(event.target.value)}>
          {availableTechnicians.length === 0 ? <option value="">All technicians are clocked in</option> : availableTechnicians.map(technician => <option key={technician.id} value={technician.id}>{technician.fullName}</option>)}
        </select></label>
        <button type="button" disabled={busy || !selectedTechnicianId} onClick={() => void clockIn()}>{busy ? 'Saving…' : 'Clock in'}</button>
      </div>
    </section>

    <section className="timekeeping-period">
      <div><p className="timekeeping-eyebrow">Shift history</p><h2>Recorded hours</h2></div>
      <label>From<input type="date" value={fromDate} max={toDate} onChange={event => setFromDate(event.target.value)} /></label>
      <label>To<input type="date" value={toDate} min={fromDate} onChange={event => setToDate(event.target.value)} /></label>
    </section>

    <div className="timekeeping-summary">
      <article><span>Clocked hours</span><strong>{(data?.totalHours ?? 0).toFixed(2)}</strong><small>Within selected dates</small></article>
      <article><span>Open shifts</span><strong>{data?.openShifts ?? 0}</strong><small>Awaiting clock-out</small></article>
      <article><span>Recorded shifts</span><strong>{data?.entries.length ?? 0}</strong><small>Within selected dates</small></article>
    </div>

    <section className="timekeeping-table-section">
      <div className="timekeeping-table-heading"><div><h2>Technician shifts</h2><p>{fromDate} to {toDate}</p></div><button type="button" disabled={loading || busy} onClick={() => void load()}>Refresh</button></div>
      <div className="timekeeping-table-scroll"><table><thead><tr><th>Technician</th><th>Clock in</th><th>Recorded by</th><th>Clock out</th><th>Recorded by</th><th>Hours</th><th>Action</th></tr></thead>
        <tbody>{data?.entries.length ? data.entries.map(entry => <tr key={entry.id}>
          <td>{entry.technicianName}</td><td>{formatDateTime(entry.clockInAt)}</td><td>{entry.clockInRecordedBy}</td>
          <td>{formatDateTime(entry.clockOutAt)}</td><td>{entry.clockOutRecordedBy ?? '—'}</td><td>{entry.hoursWorked.toFixed(2)}</td>
          <td>{entry.isOpen ? <button className="timekeeping-clock-out" type="button" disabled={busy} onClick={() => void clockOut(entry)}>Clock out</button> : <span className="timekeeping-closed">Complete</span>}</td>
        </tr>) : <tr><td colSpan={7} className="timekeeping-empty">No technician shifts recorded for these dates.</td></tr>}</tbody>
      </table></div>
    </section>
  </main>
}
