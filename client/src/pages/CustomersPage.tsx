import { useEffect, useState, type FormEvent } from 'react'
import { isAxiosError } from 'axios'
import {
  addCreditProductNote,
  createCustomer,
  deleteCreditProductNote,
  getCustomer,
  getLedger,
  listCustomers,
  recordCreditSale,
  recordPayment,
  updateCreditProductNote,
} from '../services/repairsService'
import type { Customer, LedgerEntry } from '../types/phase6'
import './repairs.css'

const message = (error: unknown) => isAxiosError(error)
  ? (typeof error.response?.data === 'string' ? error.response.data : error.response?.data?.message ?? error.response?.data?.title ?? 'Something went wrong.')
  : 'Something went wrong.'
const money = (value: number) => `KES ${value.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`

export function CustomersPage() {
  const [customers, setCustomers] = useState<Customer[]>([])
  const [selected, setSelected] = useState<Customer | null>(null)
  const [ledger, setLedger] = useState<LedgerEntry[]>([])
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [customerForm, setCustomerForm] = useState({ fullName: '', phoneNumber: '', email: '' })
  const [credit, setCredit] = useState({ amount: '', products: [''] })
  const [payment, setPayment] = useState({ amount: '', paymentMethod: 'Cash', notes: '' })
  const [noteDrafts, setNoteDrafts] = useState<Record<string, string>>({})
  const [editingNote, setEditingNote] = useState<{ transactionId: string; noteId: string } | null>(null)
  const [editValue, setEditValue] = useState('')

  const load = async () => {
    try { setCustomers(await listCustomers()) } catch (e) { setError(message(e)) }
  }

  const select = async (customer: Customer) => {
    setError(null)
    try {
      const [full, entries] = await Promise.all([getCustomer(customer.id), getLedger(customer.id)])
      setSelected(full)
      setLedger(entries)
    } catch (e) { setError(message(e)) }
  }

  useEffect(() => { void load() }, [])

  const addCustomer = async (event: FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const customer = await createCustomer(customerForm)
      setCustomerForm({ fullName: '', phoneNumber: '', email: '' })
      await load()
      await select(customer)
    } catch (e) { setError(message(e)) } finally { setBusy(false) }
  }

  const postCredit = async (event: FormEvent) => {
    event.preventDefault()
    if (!selected || !credit.amount) return
    const products = credit.products.map(item => item.trim()).filter(Boolean)
    if (products.length === 0) {
      setError('Add at least one product taken on credit.')
      return
    }
    setBusy(true)
    setError(null)
    try {
      await recordCreditSale(selected.id, Number(credit.amount), products)
      setCredit({ amount: '', products: [''] })
      await select(selected)
      await load()
    } catch (e) { setError(message(e)) } finally { setBusy(false) }
  }

  const postPayment = async (event: FormEvent) => {
    event.preventDefault()
    if (!selected || !payment.amount) return
    setBusy(true)
    setError(null)
    try {
      await recordPayment(selected.id, Number(payment.amount), payment.paymentMethod, payment.notes)
      setPayment({ amount: '', paymentMethod: 'Cash', notes: '' })
      await select(selected)
      await load()
    } catch (e) { setError(message(e)) } finally { setBusy(false) }
  }

  const refreshLedger = async () => { if (selected) await select(selected) }

  const addNote = async (entry: LedgerEntry) => {
    if (!selected) return
    const content = noteDrafts[entry.id]?.trim()
    if (!content) return
    setBusy(true)
    setError(null)
    try {
      await addCreditProductNote(selected.id, entry.id, content)
      setNoteDrafts(current => ({ ...current, [entry.id]: '' }))
      await refreshLedger()
    } catch (e) { setError(message(e)) } finally { setBusy(false) }
  }

  const saveNote = async () => {
    if (!selected || !editingNote || !editValue.trim()) return
    setBusy(true)
    setError(null)
    try {
      await updateCreditProductNote(selected.id, editingNote.transactionId, editingNote.noteId, editValue.trim())
      setEditingNote(null)
      setEditValue('')
      await refreshLedger()
    } catch (e) { setError(message(e)) } finally { setBusy(false) }
  }

  const removeNote = async (entry: LedgerEntry, noteId: string) => {
    if (!selected || !window.confirm('Delete this product note?')) return
    setBusy(true)
    setError(null)
    try {
      await deleteCreditProductNote(selected.id, entry.id, noteId)
      await refreshLedger()
    } catch (e) { setError(message(e)) } finally { setBusy(false) }
  }

  return (
    <main className="service-page">
      <header className="service-header">
        <div>
          <p className="service-eyebrow">Customer accounts</p>
          <h1 className="service-title">Deni ledger</h1>
          <p className="service-subtitle">Keep customer credit sales and payments clear, accurate, and traceable.</p>
        </div>
        <button className="service-button service-button--quiet" onClick={() => void load()}>Refresh</button>
      </header>
      {error && <p className="service-alert" role="alert">{error}</p>}

      <div className="service-grid">
        <section className="service-card">
          <h2>Add customer</h2>
          <form className="service-form" onSubmit={e => void addCustomer(e)}>
            <label>Full name<input required value={customerForm.fullName} onChange={e => setCustomerForm({ ...customerForm, fullName: e.target.value })} /></label>
            <label>Phone number<input required placeholder="2547…" value={customerForm.phoneNumber} onChange={e => setCustomerForm({ ...customerForm, phoneNumber: e.target.value })} /></label>
            <label>Email (optional)<input type="email" value={customerForm.email} onChange={e => setCustomerForm({ ...customerForm, email: e.target.value })} /></label>
            <button className="service-button" disabled={busy}>Save customer</button>
          </form>
          <h2 className="customer-list-heading">Customers</h2>
          <div className="customer-list">
            {customers.map(customer => (
              <button key={customer.id} className={`customer-select ${selected?.id === customer.id ? 'customer-select--active' : ''}`} onClick={() => void select(customer)}>
                {customer.fullName}<small>{customer.phone ?? 'No phone'} · Balance {money(customer.currentCreditBalance)}</small>
              </button>
            ))}
          </div>
        </section>

        <section className="service-card">
          <h2>{selected ? selected.fullName : 'Select a customer'}</h2>
          {selected ? <>
            <div className="ledger-summary"><span>Current balance</span><strong>{money(selected.currentCreditBalance)}</strong></div>

            <form className="customer-transaction-form" onSubmit={e => void postCredit(e)}>
              <div className="customer-form-heading"><div><span className="service-eyebrow">New credit sale</span><p>Record the amount and each product the customer takes.</p></div></div>
              <label className="customer-credit-amount">Amount (KES)<input required type="number" min="0.01" step="0.01" value={credit.amount} onChange={e => setCredit({ ...credit, amount: e.target.value })} /></label>
              <div className="credit-product-fields">
                <div className="credit-product-heading"><span>Products taken</span><span>Up to 300 characters per note</span></div>
                {credit.products.map((item, index) => (
                  <div className="credit-product-input" key={index}>
                    <input required={index === 0} maxLength={300} placeholder={`Product ${index + 1} or details`} value={item} onChange={e => setCredit(current => ({ ...current, products: current.products.map((value, itemIndex) => itemIndex === index ? e.target.value : value) }))} />
                    {credit.products.length > 1 && <button type="button" className="customer-note-remove" aria-label={`Remove product ${index + 1}`} onClick={() => setCredit(current => ({ ...current, products: current.products.filter((_, itemIndex) => itemIndex !== index) }))}>Remove</button>}
                  </div>
                ))}
                <button type="button" className="customer-add-note" onClick={() => setCredit(current => ({ ...current, products: [...current.products, ''] }))}>+ Add another product</button>
              </div>
              <button className="service-button" disabled={busy}>Add credit sale</button>
            </form>

            <form className="customer-transaction-form customer-payment-form" onSubmit={e => void postPayment(e)}>
              <span className="service-eyebrow">Record a payment</span>
              <div className="customer-payment-fields">
                <label>Amount (KES)<input required type="number" min="0.01" step="0.01" value={payment.amount} onChange={e => setPayment({ ...payment, amount: e.target.value })} /></label>
                <label>Method<select value={payment.paymentMethod} onChange={e => setPayment({ ...payment, paymentMethod: e.target.value })}><option>Cash</option><option>M-Pesa</option><option>Bank transfer</option></select></label>
                <label>Note (optional)<input value={payment.notes} onChange={e => setPayment({ ...payment, notes: e.target.value })} /></label>
                <button className="service-button service-button--quiet" disabled={busy}>Record payment</button>
              </div>
            </form>

            <div className="customer-ledger-heading"><div><span className="service-eyebrow">Account activity</span><p>Product notes stay attached to their credit sale.</p></div></div>
            <div className="customer-ledger-scroll">
              <table className="ledger-table customer-ledger-table">
                <thead><tr><th>When</th><th>Type</th><th>Amount</th><th>Balance after</th><th>Products / notes</th></tr></thead>
                <tbody>{ledger.map(entry => <tr key={entry.id}>
                  <td>{new Date(entry.timestamp).toLocaleString()}</td>
                  <td className={entry.type === 'CreditSale' ? 'credit-positive' : 'credit-payment'}>{entry.type === 'CreditSale' ? 'Credit sale' : entry.paymentMethod ?? 'Payment'}</td>
                  <td>{money(entry.amount)}</td>
                  <td>{money(entry.balanceAfter)}</td>
                  <td className="customer-notes-cell">
                    {entry.notes && <p className="customer-legacy-note">{entry.notes}</p>}
                    {(entry.productNotes ?? []).map(note => <div className="customer-product-note" key={note.id}>
                      {editingNote?.noteId === note.id ? <div className="customer-note-edit"><input maxLength={300} value={editValue} onChange={e => setEditValue(e.target.value)} /><button type="button" onClick={() => void saveNote()} disabled={busy}>Save</button><button type="button" onClick={() => setEditingNote(null)}>Cancel</button></div>
                        : <><span>{note.content}</span><div className="customer-note-actions"><button type="button" onClick={() => { setEditingNote({ transactionId: entry.id, noteId: note.id }); setEditValue(note.content) }}>Edit</button><button type="button" className="customer-note-delete" onClick={() => void removeNote(entry, note.id)} disabled={busy}>Delete</button></div></>}
                    </div>)}
                    {entry.type === 'CreditSale' && <div className="customer-add-note-row"><input maxLength={300} placeholder="Add a product or note" value={noteDrafts[entry.id] ?? ''} onChange={e => setNoteDrafts(current => ({ ...current, [entry.id]: e.target.value }))} /><button type="button" onClick={() => void addNote(entry)} disabled={busy || !(noteDrafts[entry.id] ?? '').trim()}>Add note</button></div>}
                  </td>
                </tr>)}</tbody>
              </table>
            </div>
            {ledger.length === 0 && <p className="service-empty">No credit activity for this customer.</p>}
          </> : <p className="service-empty">Choose a customer to view their balance and ledger.</p>}
        </section>
      </div>
    </main>
  )
}
