import { useEffect, useRef, useState, type FormEvent } from 'react'
import { isAxiosError } from 'axios'
import { useAuth } from '../hooks/useAuth'
import {
  assignRepair,
  createRepair,
  getRepairReceipt,
  listCustomers,
  listRepairs,
  listTechnicians,
  myRepairQueue,
  recordRepairPayment,
  setRepairFinalCost,
  updateRepairStatus,
} from '../services/repairsService'
import { ReceiptPreview, type ReceiptData } from '../components/receipt/ReceiptSheet'
import { buildRepairReceipt } from '../utils/receiptBuilders'
import { MpesaWaitingModal } from '../components/MpesaWaitingModal'
import { getRepairPaymentStatus, initiateRepairMpesaPayment } from '../services/mpesaService'
import type { Customer, Repair, RepairStatus, TechnicianSummary } from '../types/phase6'
import './repairs.css'

const STATUSES: RepairStatus[] = ['Received', 'Diagnosing', 'AwaitingParts', 'InRepair', 'Ready', 'Collected']
const PAYMENT_METHODS = ['Cash', 'M-Pesa', 'Bank transfer', 'Card']
const BANKS = ['Absa Bank', 'Co-operative Bank']

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
  const [visibleDiagnosisNotes, setVisibleDiagnosisNotes] = useState<Record<string, boolean>>({})
  const [techIds, setTechIds] = useState<Record<string, string>>({})
  const [costInputs, setCostInputs] = useState<Record<string, string>>({})
  const [paymentAmounts, setPaymentAmounts] = useState<Record<string, string>>({})
  const [paymentMethods, setPaymentMethods] = useState<Record<string, string>>({})
  const [repairMpesaModes, setRepairMpesaModes] = useState<Record<string, 'manual' | 'stk'>>({})
  const [paymentBanks, setPaymentBanks] = useState<Record<string, string>>({})
  const [paymentReferences, setPaymentReferences] = useState<Record<string, string>>({})
  const [manualMpesaVerified, setManualMpesaVerified] = useState<Record<string, boolean>>({})
  const [mpesaTarget, setMpesaTarget] = useState<{ repair: Repair; amount: number } | null>(null)
  const [receiptView, setReceiptView] = useState<ReceiptData | null>(null)
  const mpesaPaymentId = useRef<string | null>(null)
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
      await updateRepairStatus(repair.id, status, notes[repair.id] ?? repair.diagnosisNotes ?? '')
      setNotes((current) => {
        const next = { ...current }
        delete next[repair.id]
        return next
      })
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

  const openReceipt = async (repairId: string) => {
    try {
      setReceiptView(buildRepairReceipt(await getRepairReceipt(repairId)))
    } catch (e) {
      setRowError((prev) => ({ ...prev, [repairId]: errorText(e) }))
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
    const method = paymentMethods[repair.id] ?? PAYMENT_METHODS[0]
    const manualMpesa = method === 'M-Pesa' && repairMpesaModes[repair.id] === 'manual'
    if (method === 'M-Pesa' && !manualMpesa) {
      // Real STK Push: the balance only changes once Safaricom confirms (server-side).
      setMpesaTarget({ repair, amount: Math.round(amount) })
      return
    }
    if (method === 'Bank transfer' && (!paymentBanks[repair.id] || !paymentReferences[repair.id]?.trim())) {
      setRowError((prev) => ({ ...prev, [repair.id]: 'Choose a bank and enter the transfer reference.' }))
      return
    }
    if (method === 'Bank transfer' && !manualMpesaVerified[repair.id]) {
      setRowError((prev) => ({ ...prev, [repair.id]: 'Confirm the transfer arrived before recording it.' }))
      return
    }
    if (manualMpesa && !paymentReferences[repair.id]?.trim()) {
      setRowError((prev) => ({ ...prev, [repair.id]: 'Enter the M-Pesa receipt code after confirming the payment.' }))
      return
    }
      if (manualMpesa && !paymentBanks[repair.id]) {
        setRowError((prev) => ({ ...prev, [repair.id]: 'Choose the bank used for the M-Pesa payment.' }))
        return
      }
    setBusy(true)
    try {
      if (manualMpesa && !manualMpesaVerified[repair.id]) {
        setRowError((prev) => ({ ...prev, [repair.id]: 'Confirm the payment was received before recording it.' }))
        return
      }
      await recordRepairPayment(
        repair.id,
        amount,
        method,
        undefined,
          method === 'Cash' || method === 'Bank transfer' || manualMpesa ? paymentBanks[repair.id] || null : null,
        paymentReferences[repair.id] || null,
        manualMpesaVerified[repair.id] ?? false,
        manualMpesaVerified[repair.id] ?? false,
      )
      setPaymentAmounts((prev) => ({ ...prev, [repair.id]: '' }))
      setPaymentReferences((prev) => ({ ...prev, [repair.id]: '' }))
      setManualMpesaVerified((prev) => ({ ...prev, [repair.id]: false }))
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
                      <textarea
                        className="repair-notes-input"
                        aria-label={`Diagnosis notes for ${repair.ticketNumber}`}
                        placeholder="Add diagnosis notes…"
                        value={notes[repair.id] ?? repair.diagnosisNotes ?? ''}
                        onChange={(e) => setNotes({ ...notes, [repair.id]: e.target.value })}
                      />
                      {canManage && (
                        <button className="service-button service-button--quiet" onClick={() => void setStatus(repair, repair.status)} disabled={busy}>
                          Save notes
                        </button>
                      )}
                    </div>

                    <button
                      type="button"
                      className="service-button service-button--quiet repair-notes-toggle"
                      aria-expanded={Boolean(visibleDiagnosisNotes[repair.id])}
                      onClick={() => setVisibleDiagnosisNotes((current) => ({ ...current, [repair.id]: !current[repair.id] }))}
                    >
                      {visibleDiagnosisNotes[repair.id] ? 'Hide diagnosis notes' : 'Diagnosis notes'}
                    </button>
                    {visibleDiagnosisNotes[repair.id] && (
                      <section className="repair-notes" aria-label={`Saved diagnosis notes for ${repair.ticketNumber}`}>
                        <div className="repair-notes__heading">
                          <span>Saved diagnosis notes</span>
                          <span>{repair.diagnosisNotes ? 'Saved' : 'No note yet'}</span>
                        </div>
                        <p className={!repair.diagnosisNotes ? 'repair-notes__empty' : ''}>
                          {repair.diagnosisNotes || 'No diagnosis notes have been saved for this repair.'}
                        </p>
                      </section>
                    )}

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
                                onChange={(e) => {
                                  setPaymentMethods({ ...paymentMethods, [repair.id]: e.target.value })
                                  setPaymentReferences({ ...paymentReferences, [repair.id]: '' })
                                  setPaymentBanks({ ...paymentBanks, [repair.id]: '' })
                                  setManualMpesaVerified({ ...manualMpesaVerified, [repair.id]: false })
                                }}
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
                          {!isPaidInFull && (paymentMethods[repair.id] ?? PAYMENT_METHODS[0]) === 'Cash' && (
                            <div className="service-inline repair-payment-details">
                              <select aria-label="Cash destination" value={paymentBanks[repair.id] ?? ''} onChange={(e) => setPaymentBanks({ ...paymentBanks, [repair.id]: e.target.value })}>
                                <option value="">Cash drawer</option>
                                {BANKS.map((bank) => <option key={bank}>{bank}</option>)}
                              </select>
                              <span />
                              <span className="repair-payment-hint">Choose a bank if cash was deposited directly.</span>
                            </div>
                          )}
                          {!isPaidInFull && (paymentMethods[repair.id] ?? PAYMENT_METHODS[0]) === 'Bank transfer' && (
                            <div className="service-inline repair-payment-details">
                              <select aria-label="Receiving bank" required value={paymentBanks[repair.id] ?? ''} onChange={(e) => { setPaymentBanks({ ...paymentBanks, [repair.id]: e.target.value }); setManualMpesaVerified({ ...manualMpesaVerified, [repair.id]: false }) }}>
                                <option value="">Choose bank</option>
                                {BANKS.map((bank) => <option key={bank}>{bank}</option>)}
                              </select>
                              <input placeholder="Bank transfer reference" value={paymentReferences[repair.id] ?? ''} onChange={(e) => { setPaymentReferences({ ...paymentReferences, [repair.id]: e.target.value }); setManualMpesaVerified({ ...manualMpesaVerified, [repair.id]: false }) }} />
                              <label className="repair-payment-confirmation">
                                <input type="checkbox" checked={Boolean(manualMpesaVerified[repair.id])} onChange={(e) => setManualMpesaVerified({ ...manualMpesaVerified, [repair.id]: e.target.checked })} />
                                Transfer confirmed
                              </label>
                            </div>
                          )}
                          {!isPaidInFull && (paymentMethods[repair.id] ?? PAYMENT_METHODS[0]) === 'M-Pesa' && (
                            <div className="service-inline repair-payment-details">
                              <select aria-label="M-Pesa payment type" value={repairMpesaModes[repair.id] ?? 'stk'} onChange={(e) => setRepairMpesaModes({ ...repairMpesaModes, [repair.id]: e.target.value as 'manual' | 'stk' })}>
                                <option value="stk">Send STK Push</option>
                                <option value="manual">Customer already paid</option>
                              </select>
                              {repairMpesaModes[repair.id] === 'manual' ? (
                                  <>
                                    <select aria-label="Bank used for M-Pesa payment" required value={paymentBanks[repair.id] ?? ''} onChange={(e) => { setPaymentBanks({ ...paymentBanks, [repair.id]: e.target.value }); setManualMpesaVerified({ ...manualMpesaVerified, [repair.id]: false }) }}>
                                      <option value="">Choose bank used</option>
                                      {BANKS.map((bank) => <option key={bank}>{bank}</option>)}
                                    </select>
                                    <input placeholder="M-Pesa receipt code" value={paymentReferences[repair.id] ?? ''} onChange={(e) => { setPaymentReferences({ ...paymentReferences, [repair.id]: e.target.value }); setManualMpesaVerified({ ...manualMpesaVerified, [repair.id]: false }) }} />
                                  </>
                              ) : <span />}
                              {repairMpesaModes[repair.id] === 'manual' ? (
                                <label className="repair-payment-confirmation">
                                  <input type="checkbox" checked={Boolean(manualMpesaVerified[repair.id])} onChange={(e) => setManualMpesaVerified({ ...manualMpesaVerified, [repair.id]: e.target.checked })} />
                                  Confirmed received
                                </label>
                              ) : <span />}
                            </div>
                          )}
                        </>
                      )}
                      <button className="service-button service-button--quiet" onClick={() => void openReceipt(repair.id)}>
                        {repair.finalCost === null ? 'Print intake slip' : 'Receipt'}
                      </button>
                      {rowError[repair.id] && <p className="service-alert service-alert--row">{rowError[repair.id]}</p>}
                    </div>
                  </article>
                )
              })
            )}
          </div>
        </section>
      </div>

      {receiptView && (
        <div className="rcpt-modal-backdrop" role="dialog" aria-modal="true">
          <div className="rcpt-modal">
            <ReceiptPreview data={receiptView} />
            <button type="button" className="rcpt-close-btn" onClick={() => setReceiptView(null)}>Close</button>
          </div>
        </div>
      )}

      {mpesaTarget && (
        <MpesaWaitingModal
          key={mpesaTarget.repair.id}
          amount={mpesaTarget.amount}
          initiate={async (phone) => {
            const res = await initiateRepairMpesaPayment(mpesaTarget.repair.id, mpesaTarget.amount, phone)
            mpesaPaymentId.current = res.paymentId
          }}
          checkStatus={() => getRepairPaymentStatus(mpesaTarget.repair.id, mpesaPaymentId.current!)}
          failureNote="No money was recorded against this repair. Retry, or take the payment another way."
          onResolved={(finalStatus) => {
            setMpesaTarget(null)
            mpesaPaymentId.current = null
            if (finalStatus === 'Success') {
              setPaymentAmounts((prev) => ({ ...prev, [mpesaTarget.repair.id]: '' }))
              void openReceipt(mpesaTarget.repair.id)
            }
            void load()
          }}
        />
      )}
    </main>
  )
}
