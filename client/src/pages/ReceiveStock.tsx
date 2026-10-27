import { useEffect, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { isAxiosError } from 'axios'
import { getProduct, listProducts } from '../services/ProductsService'
import { listCategories } from '../services/categoriesService'
import { receiveBulkStock, receiveSerialStock, sourceStock } from '../services/stockService'
import type { Category, Product } from '../types/product'
import type { ApiErrorBody } from '../types/auth'
import './ReceiveStock.css'

const PAGE_SIZE = 20

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
    const navigate = useNavigate()
    const { user } = useAuth()
    const isCashier = user?.role === 'Cashier'

    const [categories, setCategories] = useState<Category[]>([])
    const [selected, setSelected] = useState<Product | null>(null)

    // Product list state — replaces the old search-only flow.
    const [products, setProducts] = useState<Product[]>([])
    const [total, setTotal] = useState(0)
    const [page, setPage] = useState(1)
    const [query, setQuery] = useState('')
    const [loading, setLoading] = useState(true)
    const [loadError, setLoadError] = useState<string | null>(null)

    // Receiving form state (unchanged from before).
    const [serialInput, setSerialInput] = useState('')
    const [serials, setSerials] = useState<string[]>([])
    const [bulkQuantity, setBulkQuantity] = useState(searchParams.get('quantity') ?? '')
    const [submitting, setSubmitting] = useState(false)
    const [error, setError] = useState<string | null>(null)
    const [successMessage, setSuccessMessage] = useState<string | null>(null)
    const [mode, setMode] = useState<'standard' | 'source'>(searchParams.get('mode') === 'source' || user?.role === 'Cashier' ? 'source' : 'standard')
    const [supplierName, setSupplierName] = useState('')
    const [unitCost, setUnitCost] = useState('')
    const [purchaseReference, setPurchaseReference] = useState('')
    const [purchaseTransactionId, setPurchaseTransactionId] = useState(() => crypto.randomUUID())

    useEffect(() => {
        void listCategories().then(setCategories)
    }, [])

    useEffect(() => {
        if (isCashier || searchParams.get('mode') === 'source') setMode('source')
    }, [isCashier, searchParams])

    // Arriving from ProductsPage's "Receive stock →" link with ?productId=...
    // preselects that product directly — same behaviour as before.
    useEffect(() => {
        const productId = searchParams.get('productId')
        if (productId) {
            void getProduct(productId).then(setSelected).catch(() => {
                setError('Could not load the linked product — try selecting it from the list below.')
            })
        }
    }, [searchParams])

    // Load the product list. Debounced when searching; immediate on page change.
    useEffect(() => {
        const trimmed = query.trim()
        const timeout = setTimeout(() => {
            setLoading(true)
            setLoadError(null)
            listProducts({
                page,
                pageSize: PAGE_SIZE,
                search: trimmed || undefined,
            })
                .then((res) => {
                    setProducts(res.items)
                    setTotal(res.total)
                })
                .catch((err) => setLoadError(getErrorMessage(err, 'Could not load products.')))
                .finally(() => setLoading(false))
        }, trimmed ? 250 : 0)

        return () => clearTimeout(timeout)
    }, [query, page])

    const selectProduct = (product: Product) => {
        setSelected(product)
        setSerials([])
        setSerialInput('')
        setBulkQuantity('')
        setError(null)
        setSuccessMessage(null)
        setPurchaseTransactionId(crypto.randomUUID())
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

    const submitSourced = async () => {
        if (!selected) return
        const quantity = isSerialized ? serials.length : Number(bulkQuantity)
        const cost = Number(unitCost)
        if (!supplierName.trim()) { setError('Enter the neighboring shop or supplier name.'); return }
        if (!Number.isFinite(cost) || cost < 0) { setError('Enter a valid unit purchase cost.'); return }
        if (!Number.isInteger(quantity) || quantity <= 0) { setError(isSerialized ? 'Add a serial number for each unit.' : 'Enter a quantity greater than zero.'); return }

        setSubmitting(true)
        setError(null)
        try {
            const result = await sourceStock({
                clientTransactionId: purchaseTransactionId,
                productId: selected.id,
                supplierName: supplierName.trim(),
                quantity,
                unitCost: cost,
                serialNumbers: isSerialized ? serials : undefined,
                reference: purchaseReference.trim() || null,
            })
            setSuccessMessage(`Recorded ${quantity} × ${selected.name} from ${supplierName.trim()} for ${result.totalCost.toLocaleString('en-KE', { minimumFractionDigits: 2 })} KES. ${result.newQuantity} now in stock.`)
            setSerials([])
            setSerialInput('')
            setBulkQuantity('')
            setSupplierName('')
            setUnitCost('')
            setPurchaseReference('')
            setPurchaseTransactionId(crypto.randomUUID())
            if (searchParams.get('returnTo') === 'checkout') navigate('/checkout')
        } catch (err) {
            setError(getErrorMessage(err, 'Could not record the sourced purchase. Check the details and try again.'))
        } finally {
            setSubmitting(false)
        }
    }

    const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

    return (
        <div className="receive-stock-screen">
            <h1 className="receive-stock-title">Receive stock</h1>

            {!selected && (
                <>
                    <div className="receive-stock-search-row">
                        <input
                            type="text"
                            placeholder="Filter products by SKU, name, or barcode"
                            value={query}
                            onChange={(e) => {
                                setPage(1)
                                setQuery(e.target.value)
                            }}
                        />
                    </div>

                    {loading && <p className="receive-stock-hint">Loading products…</p>}
                    {loadError && (
                        <p className="receive-stock-error" role="alert">{loadError}</p>
                    )}

                    {!loading && !loadError && (
                        <>
                            <table className="receive-stock-table">
                                <thead>
                                <tr>
                                    <th>SKU</th>
                                    <th>Name</th>
                                    <th>Category</th>
                                    <th className="receive-stock-col-stock">Stock</th>
                                    <th></th>
                                </tr>
                                </thead>
                                <tbody>
                                {products.map((p) => (
                                    <tr key={p.id}>
                                        <td>{p.sku}</td>
                                        <td>{p.name}</td>
                                        <td>{p.categoryName}</td>
                                        <td className="receive-stock-col-stock">{p.stockCount}</td>
                                        <td className="receive-stock-col-action">
                                            <button
                                                type="button"
                                                className="receive-stock-select-btn"
                                                onClick={() => selectProduct(p)}
                                            >
                                                Select →
                                            </button>
                                        </td>
                                    </tr>
                                ))}
                                {products.length === 0 && (
                                    <tr>
                                        <td colSpan={5} className="receive-stock-hint">
                                            No products found.
                                        </td>
                                    </tr>
                                )}
                                </tbody>
                            </table>

                            {totalPages > 1 && (
                                <div className="receive-stock-pagination">
                                    <button
                                        type="button"
                                        disabled={page <= 1}
                                        onClick={() => setPage((p) => p - 1)}
                                    >
                                        ← Prev
                                    </button>
                                    <span>Page {page} of {totalPages}</span>
                                    <button
                                        type="button"
                                        disabled={page >= totalPages}
                                        onClick={() => setPage((p) => p + 1)}
                                    >
                                        Next →
                                    </button>
                                </div>
                            )}
                        </>
                    )}
                </>
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

                    {!isCashier && <div className="receive-stock-modes" role="group" aria-label="Stock receiving type">
                        <button type="button" className={mode === 'standard' ? 'is-active' : ''} onClick={() => setMode('standard')}>Regular stock</button>
                        <button type="button" className={mode === 'source' ? 'is-active' : ''} onClick={() => setMode('source')}>Bought from another shop</button>
                    </div>}

                    {successMessage && <p className="receive-stock-success">{successMessage}</p>}
                    {error && <p className="receive-stock-error" role="alert">{error}</p>}

                    {mode === 'source' && <div className="receive-stock-source-fields">
                        <label>Supplier / neighboring shop<input value={supplierName} onChange={e => setSupplierName(e.target.value)} maxLength={160} placeholder="Shop name" /></label>
                        <label>Purchase cost per unit (KES)<input type="number" min="0" step="0.01" value={unitCost} onChange={e => setUnitCost(e.target.value)} placeholder="What you paid per item" /></label>
                        <label>Receipt / reference (optional)<input value={purchaseReference} onChange={e => setPurchaseReference(e.target.value)} maxLength={120} placeholder="Reference or note" /></label>
                        <p className="receive-stock-hint">The actual supplier cost is saved and used for profit reports when the item sells.</p>
                    </div>}

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
                                            <button
                                                type="button"
                                                onClick={() => removeSerial(s)}
                                                aria-label={`Remove ${s}`}
                                            >
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
                                onClick={() => void (mode === 'source' ? submitSourced() : submitSerial())}
                            >
                                {submitting ? 'Saving…' : mode === 'source' ? `Record purchase & receive ${serials.length || ''} unit(s)`.trim() : `Receive ${serials.length || ''} unit(s)`.trim()}
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
                                    onClick={() => void (mode === 'source' ? submitSourced() : submitBulk())}
                                >
                                    {submitting ? 'Saving…' : mode === 'source' ? 'Record purchase & receive' : 'Receive'}
                                </button>
                            </div>
                        </div>
                    )}
                </div>
            )}
        </div>
    )
}
