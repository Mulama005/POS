import { useEffect, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { isAxiosError } from 'axios'
import { getProduct, searchProducts } from '../services/ProductsService'
import { listCategories } from '../services/categoriesService'
import { receiveBulkStock, receiveSerialStock } from '../services/stockService'
import type { Category, Product, ProductSummary } from '../types/product'
import type { ApiErrorBody } from '../types/auth'
import './ReceiveStock.css'

function getErrorMessage(err: unknown, fallback: string): string {
  if (isAxiosError<ApiErrorBody>(err) && err.response?.data?.message) {
    return err.response.data.message
  }
  if (isAxiosError(err) && typeof err.response?.data === 'string') {
    return err.response.data
  }
  return fallback
}

export function ReceiveStock() {
  const [searchParams] = useSearchParams()

  const [categories, setCategories] = useState<Category[]>([])
  const [selected, setSelected] = useState<Product | ProductSummary | null>(null)
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<ProductSummary[]>([])
  const [searching, setSearching] = useState(false)

  const [serialInput, setSerialInput] = useState('')
  const [serials, setSerials] = useState<string[]>([])
  const [bulkQuantity, setBulkQuantity] = useState('')

  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [successMessage, setSuccessMessage] = useState<string | null>(null)

  useEffect(() => {
    void listCategories().then(setCategories)
  }, [])

  // Arriving from ProductsPage's "Receive stock →" link with ?productId=... —
  // preselect that product directly rather than making the user search again.
  useEffect(() => {
    const productId = searchParams.get('productId')
    if (productId) {
      void getProduct(productId).then(setSelected).catch(() => {
        setError('Could not load the linked product — try searching for it below.')
      })
    }
  }, [searchParams])

  useEffect(() => {
    const trimmed = query.trim()
    if (trimmed.length < 2) {
      setResults([])
      return
    }
    const timeout = setTimeout(() => {
      setSearching(true)
      searchProducts(trimmed)
        .then(setResults)
        .finally(() => setSearching(false))
    }, 250)
    return () => clearTimeout(timeout)
  }, [query])

  const selectProduct = (product: Product | ProductSummary) => {
    setSelected(product)
    setQuery('')
    setResults([])
    setSerials([])
    setSerialInput('')
    setBulkQuantity('')
    setError(null)
    setSuccessMessage(null)
  }

  const category = selected
    ? categories.find((c) => c.id === selected.categoryId) ?? null
    : null
  const isSerialized = category?.requiresSerialTracking ?? false

  const addSerial = () => {
    const trimmed = serialInput.trim()
    if (!trimmed) return
    if (serials.includes(trimmed)) {
      setError(`'${trimmed}' is already in this batch.`)
      return
    }
    setSerials((prev) => [...prev, trimmed])
    setSerialInput('')
    setError(null)
  }

  const removeSerial = (serial: string) => {
    setSerials((prev) => prev.filter((s) => s !== serial))
  }

  const submitSerial = async () => {
    if (!selected || serials.length === 0) return
    setSubmitting(true)
    setError(null)
    try {
      const res = await receiveSerialStock({ productId: selected.id, serialNumbers: serials })
      setSuccessMessage(`Received ${res.added} unit(s) of ${selected.name}.`)
      setSerials([])
    } catch (err) {
      setError(getErrorMessage(err, 'Could not receive stock.'))
    } finally {
      setSubmitting(false)
    }
  }

  const submitBulk = async () => {
    if (!selected) return
    const quantity = Number(bulkQuantity)
    if (!Number.isFinite(quantity) || quantity <= 0) {
      setError('Enter a quantity greater than zero.')
      return
    }
    setSubmitting(true)
    setError(null)
    try {
      const res = await receiveBulkStock({ productId: selected.id, quantity })
      setSuccessMessage(`${selected.name} now has ${res.newQuantity} in stock.`)
      setBulkQuantity('')
    } catch (err) {
      setError(getErrorMessage(err, 'Could not receive stock.'))
    } finally {
      setSubmitting(false)
    }
  }

  return (
    <div className="receive-stock-screen">
      <h1 className="receive-stock-title">Receive stock</h1>

      {!selected && (
        <div className="receive-stock-picker">
          <input
            type="text"
            placeholder="Search product by SKU, name, or barcode"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
          {searching && <p className="receive-stock-hint">Searching…</p>}
          {results.length > 0 && (
            <ul className="receive-stock-results">
              {results.map((p) => (
                <li key={p.id}>
                  <button type="button" onClick={() => selectProduct(p)}>
                    <span className="receive-stock-result-name">{p.name}</span>
                    <span className="receive-stock-result-meta">{p.sku} · {p.categoryName}</span>
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      )}

      {selected && (
        <div className="receive-stock-selected">
          <div className="receive-stock-selected-header">
            <div>
              <div className="receive-stock-selected-name">{selected.name}</div>
              <div className="receive-stock-selected-meta">
                {selected.sku} · {selected.categoryName} ·{' '}
                {isSerialized ? 'Serial-tracked' : 'Bulk-tracked'}
              </div>
            </div>
            <button type="button" onClick={() => setSelected(null)}>
              Change product
            </button>
          </div>

          {successMessage && <p className="receive-stock-success">{successMessage}</p>}
          {error && <p className="receive-stock-error" role="alert">{error}</p>}

          {isSerialized ? (
            <div className="receive-stock-form">
              <p className="receive-stock-hint">
                Scan or type each unit's serial number, one at a time.
              </p>
              <div className="receive-stock-serial-input-row">
                <input
                  type="text"
                  placeholder="Scan serial number"
                  value={serialInput}
                  onChange={(e) => setSerialInput(e.target.value)}
                  onKeyDown={(e) => e.key === 'Enter' && addSerial()}
                  autoFocus
                />
                <button type="button" onClick={addSerial}>Add</button>
              </div>
              {serials.length > 0 && (
                <ul className="receive-stock-serial-list">
                  {serials.map((s) => (
                    <li key={s}>
                      <span>{s}</span>
                      <button type="button" onClick={() => removeSerial(s)} aria-label={`Remove ${s}`}>
                        ×
                      </button>
                    </li>
                  ))}
                </ul>
              )}
              <button
                type="button"
                className="receive-stock-submit"
                disabled={serials.length === 0 || submitting}
                onClick={() => void submitSerial()}
              >
                {submitting ? 'Receiving…' : `Receive ${serials.length || ''} unit(s)`.trim()}
              </button>
            </div>
          ) : (
            <div className="receive-stock-form">
              <p className="receive-stock-hint">
                Bulk-tracked category — enter how many units you're adding to stock.
              </p>
              <div className="receive-stock-bulk-input-row">
                <input
                  type="number"
                  min="1"
                  placeholder="Quantity received"
                  value={bulkQuantity}
                  onChange={(e) => setBulkQuantity(e.target.value)}
                  autoFocus
                />
                <button
                  type="button"
                  className="receive-stock-submit"
                  disabled={submitting}
                  onClick={() => void submitBulk()}
                >
                  {submitting ? 'Receiving…' : 'Receive'}
                </button>
              </div>
            </div>
          )}
        </div>
      )}
    </div>
  )
}