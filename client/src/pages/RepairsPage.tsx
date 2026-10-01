import { useEffect, useState, type FormEvent } from 'react'
import { isAxiosError } from 'axios'
import { useAuth } from '../hooks/useAuth'
import {
  assignRepair,
  createRepair,
  listCustomers,
  listRepairs,
  listTechnicians,
  myRepairQueue,
  recordRepairPayment,
  setRepairFinalCost,
  updateRepairStatus,
} from '../services/repairsService'
import type { Customer, Repair, RepairStatus, TechnicianSummary } from '../types/phase6'
import './repairs.css'

const STATUSES: RepairStatus[] = ['Received', 'Diagnosing', 'AwaitingParts', 'InRepair', 'Ready', 'Collected']
const PAYMENT_METHODS = ['Cash', 'M-Pesa', 'Card']

const errorText = (error: unknown) =>
  isAxiosError(error)
    ? typeof error.response?.data === 'string'
      ? error.response.data
      : (error.response?.data?.message ?? 'The request could not be completed.')
    : 'The request could not be completed.'

const formatKes = (amount: number) => `KES ${amount.toLocaleString('en-KE', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`

export function RepairsPage() {
  const { user } = useAuth()
  const [repairs, setRepairs] = useState<Repair[]>([])
  const [customers, setCustomers] = useState<Customer[]>([])
  const [technicians, setTechnicians] = useState<TechnicianSummary[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [form, setForm] = useState({ customerId: '', deviceDescription: '', reportedFault: '', quotedCost: '' })
  const [notes, setNotes] = useState<Record<string, string>>({})
  const [techIds, setTechIds] = useState<Record<string, string>>({})
  const [costInputs, setCostInputs] = useState<Record<string, string>>({})
  const [paymentAmounts, setPaymentAmounts] = useState<Record<string, string>>({})
  const [paymentMethods, setPaymentMethods] = useState<Record<string, string>>({})
  const [rowError, setRowError] = useState<Record<string, string>>({})

  const canManage = user?.role === 'Admin' || user?.role === 'Manager'

  const load = async () => {
    setError(null)
    try {
      const [repairData, customerData, technicianData] = await Promise.all([
        canManage ? listRepairs() : myRepairQueue(),
        listCustomers(),
        canManage ? listTechnicians() : Promise.resolve([]),
      ])
      setRepairs(repairData)
      setCustomers(customerData)
      setTechnicians(technicianData)
    } catch (e) {
      setError(errorText(e))
    }
  }

  useEffect(() => {
    void load()
  }, [])

  const submit = async (event: FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      await createRepair({ ...form, quotedCost: form.quotedCost ? Number(form.quotedCost) : null })
      setForm({ customerId: '', deviceDescription: '', reportedFault: '', quotedCost: '' })
      await load()
    } catch (e) {
      setError(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  const setStatus = async (repair: Repair, status: RepairStatus) => {
    setBusy(true)
    try {
      await updateRepairStatus(repair.id, status, notes[repair.id] ?? '')
      await load()
    } catch (e) {
      setError(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  const assign = async (repair: Repair) => {
    const id = techIds[repair.id]
    if (!id) return
    setBusy(true)
    try {
      await assignRepair(repair.id, id)
      await load()
    } catch (e) {
      setError(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  const finalizeCost = async (repair: Repair) => {
    const raw = costInputs[repair.id]
    const amount = Number(raw)
    if (!raw || !(amount > 0)) {
      setRowError((prev) => ({ ...prev, [repair.id]: 'Enter a final cost greater than zero.' }))
      return
    }
    setRowError((prev) => ({ ...prev, [repair.id]: '' }))
    setBusy(true)
    try {
      await setRepairFinalCost(repair.id, amount)
      setCostInputs((prev) => ({ ...prev, [repair.id]: '' }))
      await load()
    } catch (e) {
      setRowError((prev) => ({ ...prev, [repair.id]: errorText(e) }))
    } finally {
      setBusy(false)
    }
  }

  const takePayment = async (repair: Repair) => {
    const raw = paymentAmounts[repair.id]
    const amount = Number(raw)
    if (!raw || !(amount > 0)) {
      setRowError((prev) => ({ ...prev, [repair.id]: 'Enter a payment amount greater than zero.' }))
      return
    }
    setRowError((prev) => ({ ...prev, [repair.id]: '' }))
    setBusy(true)
    try {
      await recordRepairPayment(repair.id, amount, paymentMethods[repair.id] ?? PAYMENT_METHODS[0])
      setPaymentAmounts((prev) => ({ ...prev, [repair.id]: '' }))
      await load()
    } catch (e) {
      setRowError((prev) => ({ ...prev, [repair.id]: errorText(e) }))
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="service-page">
      <header className="service-header">
        <div>
          <p className="service-eyebrow">Service desk</p>
          <h1 className="service-title">{canManage ? 'Repair management' : 'My repair queue'}</h1>
          <p className="service-subtitle">Create, assign, and keep customers informed about every repair.</p>
        </div>
        <button className="service-button service-button--quiet" onClick={() => void load()}>Refresh</button>
      </header>

      {error && <p className="service-alert">{error}</p>}

      <div className="service-grid">
        <section className="service-card">
          <h2>New repair intake</h2>
          <form className="service-form" onSubmit={(e) => void submit(e)}>
            <label>Customer
              <select required value={form.customerId} onChange={(e) => setForm({ ...form, customerId: e.target.value })}>
                <option value="">Select customer</option>
                {customers.map((c) => <option value={c.id} key={c.id}>{c.fullName} · {c.phone ?? 'no phone'}</option>)}
              </select>
            </label>
            <label>Device
              <input required value={form.deviceDescription} onChange={(e) => setForm({ ...form, deviceDescription: e.target.value })} placeholder="e.g. Samsung Galaxy A14" />
            </label>
            <label>Reported fault
              <textarea required value={form.reportedFault} onChange={(e) => setForm({ ...form, reportedFault: e.target.value })} placeholder="Describe the problem" />
            </label>
            <label>Quoted cost (KES)
              <input type="number" min="0" value={form.quotedCost} onChange={(e) => setForm({ ...form, quotedCost: e.target.value })} />
            </label>
            <button className="service-button" disabled={busy}>Create repair</button>
          </form>
        </section>

        <section className="service-card">
          <h2>{repairs.length} repair{repairs.length === 1 ? '' : 's'}</h2>
          <div className="service-list">
            {repairs.length === 0 ? (
              <p className="service-empty">No repairs to show yet.</p>
            ) : (
              repairs.map((repair) => {
                const balance = repair.balance ?? (repair.finalCost !== null ? repair.finalCost - repair.amountPaid : null)
                const isPaidInFull = balance !== null && balance <= 0
                return (
                  <article className="repair-item" key={repair.id}>
                    <div className="repair-item__top">
                      <h3>{repair.ticketNumber} · {repair.deviceDescription}</h3>
                      <span className="status-pill">{repair.status}</span>
                    </div>
                    <p>{repair.reportedFault}</p>
                    {repair.assignedTechnicianName && <p className="service-subtitle">Assigned to {repair.assignedTechnicianName}</p>}

                    <div className="repair-item__actions">
                      <select value={repair.status} disabled={busy} onChange={(e) => void setStatus(repair, e.target.value as RepairStatus)}>
                        {STATUSES.map((status) => <option key={status}>{status}</option>)}
                      </select>
                      <input
                        placeholder="Diagnosis notes (optional)"
                        value={notes[repair.id] ?? ''}
                        onChange={(e) => setNotes({ ...notes, [repair.id]: e.target.value })}
                      />
                      {canManage && (
                        <button className="service-button service-button--quiet" onClick={() => void setStatus(repair, repair.status)} disabled={busy}>
                          Save notes
                        </button>
                      )}
                    </div>

                    {canManage && (
                      <div className="service-inline">
                        <select
                          aria-label="Assign technician"
                          value={techIds[repair.id] ?? repair.assignedTechnicianId ?? ''}
                          onChange={(e) => setTechIds({ ...techIds, [repair.id]: e.target.value })}
                        >
                          <option value="">Select technician</option>
                          {technicians.map((technician) => (
                            <option value={technician.id} key={technician.id}>
                              {technician.fullName} · {technician.openRepairCount} open
                            </option>
                          ))}
                        </select>
                        <span />
                        <button
                          className="service-button service-button--quiet"
                          disabled={busy || !(techIds[repair.id] ?? repair.assignedTechnicianId)}
                          onClick={() => void assign(repair)}
                        >
                          Assign
                        </button>
                      </div>
                    )}

                    {/* --- Payment ---------------------------------------------------
                        Three states: cost not finalized yet (quote a final price,
                        which charges the customer's credit ledger); finalized with a
                        balance outstanding (take a payment against it); or settled. */}
                    <div className="repair-item__payment">
                      {repair.quotedCost !== null && (
                        <p className="repair-item__quoted-cost">Quoted: {formatKes(repair.quotedCost)}</p>
                      )}

                      {repair.finalCost === null ? (
                        <div className="service-inline">
                          <input
                            type="number"
                            min="0"
                            placeholder="Final cost (KES)"
                            value={costInputs[repair.id] ?? ''}
                            onChange={(e) => setCostInputs({ ...costInputs, [repair.id]: e.target.value })}
                          />
                          <span />
                          <button className="service-button service-button--quiet" disabled={busy} onClick={() => void finalizeCost(repair)}>
                            Finalize cost
                          </button>
                        </div>
                      ) : (
                        <>
                          <p className="repair-item__cost-summary">
                            Final: {formatKes(repair.finalCost)} · Paid: {formatKes(repair.amountPaid)}
                            {balance !== null && !isPaidInFull && <> · Balance: {formatKes(balance)}</>}
                          </p>
                          {isPaidInFull ? (
                            <span className="pos-badge pos-badge--success">Paid in full</span>
                          ) : (
                            <div className="service-inline">
                              <select
                                aria-label="Payment method"
                                value={paymentMethods[repair.id] ?? PAYMENT_METHODS[0]}
                                onChange={(e) => setPaymentMethods({ ...paymentMethods, [repair.id]: e.target.value })}
                              >
                                {PAYMENT_METHODS.map((method) => <option key={method}>{method}</option>)}
                              </select>
                              <input
                                type="number"
                                min="0"
                                placeholder={`Amount (up to ${balance !== null ? balance.toFixed(2) : ''})`}
                                value={paymentAmounts[repair.id] ?? ''}
                                onChange={(e) => setPaymentAmounts({ ...paymentAmounts, [repair.id]: e.target.value })}
                              />
                              <button className="service-button service-button--quiet" disabled={busy} onClick={() => void takePayment(repair)}>
                                Record payment
                              </button>
                            </div>
                          )}
                        </>
                      )}
                      {rowError[repair.id] && <p className="service-alert service-alert--row">{rowError[repair.id]}</p>}
                    </div>
                  </article>
                )
              })
            )}
          </div>
        </section>
      </div>
    </main>
  )
}