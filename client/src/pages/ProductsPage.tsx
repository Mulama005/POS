import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { isAxiosError } from 'axios'
import {
  createProduct,
  deleteProduct,
  listProducts,
  updateProduct,
} from '../services/ProductsService'
import { listCategories } from '../services/categoriesService'
import { registerProductWithEtims } from '../services/EtimsService'
import type { Category, Product, ProductFormValues, TaxClass } from '../types/product'
import type { ApiErrorBody } from '../types/auth'
import { formatKes } from '../utils/currency'
import { RoleGate } from '../components/RouteGuards'
import { EtimsClassificationPicker, type EtimsPickerTarget } from '../components/EtimsClassificationPicker'
import './ProductsPage.css'

type EtimsFilter = 'all' | 'classified' | 'unclassified'
/** Three real states, not just classified/unclassified — a product can be classified
 * but not yet registered, which is its own actionable state (needs the Register step
 * before it can be sold, per SalesController.Complete's eTIMS gate). */
type EtimsStatus = 'unclassified' | 'classified' | 'registered'

function getEtimsStatus(p: Product): EtimsStatus {
  if (p.etimsItemCode) return 'registered'
  if (p.etimsItemClassificationCode) return 'classified'
  return 'unclassified'
}

const TAX_CLASSES: TaxClass[] = ['Standard', 'ZeroRated', 'Exempt']
const PAGE_SIZE = 20

const EMPTY_FORM: ProductFormValues = {
  sku: '',
  barcode: '',
  name: '',
  description: '',
  categoryId: '',
  costPrice: '',
  salePrice: '',
  taxClass: 'Standard',
  reorderThreshold: '5',
  warrantyMonths: '12',
}

function getErrorMessage(err: unknown, fallback: string): string {
  if (isAxiosError<ApiErrorBody>(err) && err.response?.data?.message) {
    return err.response.data.message
  }
  if (isAxiosError(err) && typeof err.response?.data === 'string') {
    return err.response.data
  }
  return fallback
}

function toFormValues(p: Product): ProductFormValues {
  return {
    sku: p.sku,
    barcode: p.barcode ?? '',
    name: p.name,
    description: p.description ?? '',
    categoryId: p.categoryId,
    costPrice: String(p.costPrice),
    salePrice: String(p.salePrice),
    taxClass: p.taxClass,
    reorderThreshold: String(p.reorderThreshold),
    warrantyMonths: String(p.warrantyMonths),
  }
}

export function ProductsPage() {
  const [products, setProducts] = useState<Product[]>([])
  const [total, setTotal] = useState(0)
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [categories, setCategories] = useState<Category[]>([])
  const [loading, setLoading] = useState(true)
  const [loadError, setLoadError] = useState<string | null>(null)

  const [etimsFilter, setEtimsFilter] = useState<EtimsFilter>('all')
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [pickerTargets, setPickerTargets] = useState<EtimsPickerTarget[] | null>(null)

  const [formOpen, setFormOpen] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [formValues, setFormValues] = useState<ProductFormValues>(EMPTY_FORM)
  const [formImage, setFormImage] = useState<File | null>(null)
  const [formSubmitting, setFormSubmitting] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const [rowBusy, setRowBusy] = useState<Record<string, boolean>>({})
  const [rowError, setRowError] = useState<Record<string, string>>({})

  const [bulkRegistering, setBulkRegistering] = useState(false)
  const [bulkRegisterError, setBulkRegisterError] = useState<string | null>(null)

  const loadCategories = async () => {
    try {
      setCategories(await listCategories())
    } catch {
      // Non-fatal — the category select just shows nothing to pick from and
      // the create/edit form will surface a clearer error on submit.
    }
  }

  const loadProducts = async () => {
    setLoading(true)
    setLoadError(null)
    try {
      const res = await listProducts({
        page,
        pageSize: PAGE_SIZE,
        search: search || undefined,
        etimsClassified: etimsFilter === 'all' ? undefined : etimsFilter === 'classified',
      })
      setProducts(res.items)
      setTotal(res.total)
    } catch (err) {
      setLoadError(getErrorMessage(err, 'Could not load products.'))
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    void loadCategories()
  }, [])

  useEffect(() => {
    void loadProducts()
    // Selection is scoped to "the rows currently on screen" — changing page, search,
    // or the eTIMS filter can change which rows those are, so a stale selection from
    // before would silently point at products the user can no longer see.
    setSelectedIds(new Set())
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, search, etimsFilter])

  const openCreateForm = () => {
    setEditingId(null)
    setFormValues(EMPTY_FORM)
    setFormImage(null)
    setFormError(null)
    setFormOpen(true)
  }

  const openEditForm = (product: Product) => {
    setEditingId(product.id)
    setFormValues(toFormValues(product))
    setFormImage(null)
    setFormError(null)
    setFormOpen(true)
  }

  const closeForm = () => {
    setFormOpen(false)
    setEditingId(null)
  }

  const handleFormSubmit = async (e: FormEvent) => {
    e.preventDefault()
    setFormError(null)
    setFormSubmitting(true)
    try {
      if (editingId) {
        await updateProduct(editingId, formValues, formImage)
      } else {
        await createProduct(formValues, formImage)
      }
      closeForm()
      await loadProducts()
    } catch (err) {
      setFormError(getErrorMessage(err, editingId ? 'Could not update product.' : 'Could not create product.'))
    } finally {
      setFormSubmitting(false)
    }
  }

  const handleDeactivate = async (product: Product) => {
    if (!confirm(`Deactivate ${product.name}? It will no longer show up in checkout search.`)) return
    setRowError((prev) => ({ ...prev, [product.id]: '' }))
    setRowBusy((prev) => ({ ...prev, [product.id]: true }))
    try {
      await deleteProduct(product.id)
      await loadProducts()
    } catch (err) {
      setRowError((prev) => ({ ...prev, [product.id]: getErrorMessage(err, 'Could not deactivate product.') }))
    } finally {
      setRowBusy((prev) => ({ ...prev, [product.id]: false }))
    }
  }

  const handleRegister = async (product: Product) => {
    setRowError((prev) => ({ ...prev, [product.id]: '' }))
    setRowBusy((prev) => ({ ...prev, [product.id]: true }))
    try {
      await registerProductWithEtims(product.id)
      await loadProducts()
    } catch (err) {
      setRowError((prev) => ({ ...prev, [product.id]: getErrorMessage(err, 'Could not register product with eTIMS.') }))
    } finally {
      setRowBusy((prev) => ({ ...prev, [product.id]: false }))
    }
  }

  /** Registers every selected, classified-but-unregistered product one at a time —
   * there's no bulk endpoint server-side (each registration is its own auditable KRA
   * call with its own item-code sequence number), so this just sequences the same
   * per-product call the row action uses and reports how many succeeded. */
  const handleRegisterSelected = async () => {
    const targets = products.filter(
      (p) => selectedIds.has(p.id) && getEtimsStatus(p) === 'classified',
    )
    if (targets.length === 0) return

    setBulkRegisterError(null)
    setBulkRegistering(true)
    let failed = 0
    let lastError = ''
    for (const product of targets) {
      try {
        await registerProductWithEtims(product.id)
      } catch (err) {
        failed += 1
        lastError = getErrorMessage(err, 'Could not register product with eTIMS.')
      }
    }
    setBulkRegistering(false)
    setSelectedIds(new Set())
    await loadProducts()
    if (failed > 0) {
      setBulkRegisterError(
        `${targets.length - failed} of ${targets.length} registered. Last error: ${lastError}`,
      )
    }
  }

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

  const allOnPageSelected = products.length > 0 && products.every((p) => selectedIds.has(p.id))

  const toggleSelected = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) {
        next.delete(id)
      } else {
        next.add(id)
      }
      return next
    })
  }

  const toggleSelectAllOnPage = () => {
    setSelectedIds((prev) => {
      if (allOnPageSelected) {
        const next = new Set(prev)
        products.forEach((p) => next.delete(p.id))
        return next
      }
      const next = new Set(prev)
      products.forEach((p) => next.add(p.id))
      return next
    })
  }

  const toPickerTarget = (p: Product): EtimsPickerTarget => ({
    id: p.id,
    sku: p.sku,
    name: p.name,
    currentCode: p.etimsItemClassificationCode,
  })

  const openPickerForProduct = (p: Product) => setPickerTargets([toPickerTarget(p)])

  const openPickerForSelection = () => {
    const targets = products.filter((p) => selectedIds.has(p.id)).map(toPickerTarget)
    if (targets.length > 0) setPickerTargets(targets)
  }

  const closePicker = () => setPickerTargets(null)

  const handleAssigned = () => {
    setPickerTargets(null)
    setSelectedIds(new Set())
    void loadProducts()
  }

  return (
    <div className="products-screen">
      <div className="products-header">
        <h1 className="products-title">Products</h1>
        <button type="button" className="products-add-btn" onClick={formOpen ? closeForm : openCreateForm}>
          {formOpen ? 'Cancel' : '+ Add product'}
        </button>
      </div>

      {formOpen && (
        <form className="products-form" onSubmit={(e) => void handleFormSubmit(e)}>
          <div className="products-form-grid">
            <input
              type="text"
              placeholder="SKU"
              value={formValues.sku}
              onChange={(e) => setFormValues((v) => ({ ...v, sku: e.target.value }))}
              required
            />
            <input
              type="text"
              placeholder="Barcode (optional)"
              value={formValues.barcode}
              onChange={(e) => setFormValues((v) => ({ ...v, barcode: e.target.value }))}
            />
            <input
              type="text"
              placeholder="Name"
              value={formValues.name}
              onChange={(e) => setFormValues((v) => ({ ...v, name: e.target.value }))}
              required
            />
            <select
              value={formValues.categoryId}
              onChange={(e) => setFormValues((v) => ({ ...v, categoryId: e.target.value }))}
              required
            >
              <option value="" disabled>Category…</option>
              {categories.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name} {c.requiresSerialTracking ? '(serialized)' : '(bulk)'}
                </option>
              ))}
            </select>
            <input
              type="number"
              step="0.01"
              min="0"
              placeholder="Cost price (KES)"
              value={formValues.costPrice}
              onChange={(e) => setFormValues((v) => ({ ...v, costPrice: e.target.value }))}
              required
            />
            <input
              type="number"
              step="0.01"
              min="0"
              placeholder="Sale price (KES, VAT-inclusive)"
              value={formValues.salePrice}
              onChange={(e) => setFormValues((v) => ({ ...v, salePrice: e.target.value }))}
              required
            />
            <select
              value={formValues.taxClass}
              onChange={(e) => setFormValues((v) => ({ ...v, taxClass: e.target.value as TaxClass }))}
            >
              {TAX_CLASSES.map((t) => (
                <option key={t} value={t}>{t}</option>
              ))}
            </select>
            <input
              type="number"
              min="0"
              placeholder="Reorder threshold"
              value={formValues.reorderThreshold}
              onChange={(e) => setFormValues((v) => ({ ...v, reorderThreshold: e.target.value }))}
            />
            <input
              type="number"
              min="0"
              placeholder="Warranty (months)"
              value={formValues.warrantyMonths}
              onChange={(e) => setFormValues((v) => ({ ...v, warrantyMonths: e.target.value }))}
            />
            <textarea
              placeholder="Description (optional)"
              value={formValues.description}
              onChange={(e) => setFormValues((v) => ({ ...v, description: e.target.value }))}
              className="products-form-description"
            />
            <label className="products-form-image-label">
              Photo (optional)
              <input
                type="file"
                accept="image/*"
                onChange={(e) => setFormImage(e.target.files?.[0] ?? null)}
              />
            </label>
          </div>
          <div className="products-form-actions">
            <button type="submit" disabled={formSubmitting}>
              {formSubmitting ? 'Saving…' : editingId ? 'Save changes' : 'Create product'}
            </button>
          </div>
          {formError && <p className="products-error" role="alert">{formError}</p>}
        </form>
      )}

      <div className="products-search-row">
        <input
          type="text"
          placeholder="Search by SKU, name, or barcode"
          value={search}
          onChange={(e) => {
            setPage(1)
            setSearch(e.target.value)
          }}
        />
        <div className="products-etims-filter" role="group" aria-label="Filter by eTIMS classification">
          {(['all', 'classified', 'unclassified'] as const).map((f) => (
            <button
              key={f}
              type="button"
              className={`products-etims-filter-btn ${etimsFilter === f ? 'products-etims-filter-btn--active' : ''}`}
              onClick={() => {
                setPage(1)
                setEtimsFilter(f)
              }}
            >
              {f === 'all' ? 'All' : f === 'classified' ? 'Classified' : 'Unclassified'}
            </button>
          ))}
        </div>
      </div>

      {loading && <p className="products-hint">Loading products…</p>}
      {loadError && <p className="products-error" role="alert">{loadError}</p>}

      {!loading && !loadError && (
        <>
          <RoleGate roles={['Manager', 'Admin']}>
            {selectedIds.size > 0 && (
              <div className="products-selection-bar">
                <span>{selectedIds.size} product{selectedIds.size === 1 ? '' : 's'} selected</span>
                <div className="products-selection-bar-actions">
                  <button type="button" onClick={openPickerForSelection}>
                    Assign eTIMS classification
                  </button>
                  <button
                    type="button"
                    onClick={() => void handleRegisterSelected()}
                    disabled={
                      bulkRegistering ||
                      !products.some((p) => selectedIds.has(p.id) && getEtimsStatus(p) === 'classified')
                    }
                  >
                    {bulkRegistering ? 'Registering…' : 'Register with eTIMS'}
                  </button>
                  <button type="button" className="products-selection-bar-clear" onClick={() => setSelectedIds(new Set())}>
                    Clear selection
                  </button>
                </div>
              </div>
            )}
            {bulkRegisterError && (
              <p className="products-error" role="alert">{bulkRegisterError}</p>
            )}
          </RoleGate>

          <table className="products-table">
            <thead>
              <tr>
                <RoleGate roles={['Manager', 'Admin']}>
                  <th className="products-col-checkbox">
                    <input
                      type="checkbox"
                      checked={allOnPageSelected}
                      onChange={toggleSelectAllOnPage}
                      aria-label="Select all products on this page"
                    />
                  </th>
                </RoleGate>
                <th>SKU</th>
                <th>Name</th>
                <th>Category</th>
                <th>Price</th>
                <th>Tax</th>
                <th>Stock</th>
                <th>eTIMS</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {products.map((p) => (
                <tr key={p.id} className={p.isActive ? '' : 'products-row--inactive'}>
                  <RoleGate roles={['Manager', 'Admin']}>
                    <td className="products-col-checkbox">
                      <input
                        type="checkbox"
                        checked={selectedIds.has(p.id)}
                        onChange={() => toggleSelected(p.id)}
                        aria-label={`Select ${p.name}`}
                      />
                    </td>
                  </RoleGate>
                  <td>{p.sku}</td>
                  <td>{p.name}</td>
                  <td>{p.categoryName}</td>
                  <td className="products-col-price">{formatKes(p.salePrice)}</td>
                  <td className="products-col-tax">{p.taxClass}</td>
                  <td className={`products-col-stock ${p.stockCount <= p.reorderThreshold ? 'products-stock--low' : ''}`}>
                    {p.stockCount}
                  </td>
                  <td className="products-col-etims">
                    {getEtimsStatus(p) === 'registered' ? (
                      <span className="pos-badge pos-badge--success" title={`Item code ${p.etimsItemCode}`}>
                        Registered
                      </span>
                    ) : getEtimsStatus(p) === 'classified' ? (
                      <span className="pos-badge pos-badge--warn" title={p.etimsItemClassificationName ?? undefined}>
                        {p.etimsItemClassificationCode}
                      </span>
                    ) : (
                      <span className="pos-badge pos-badge--neutral">Unclassified</span>
                    )}
                  </td>
                  <td className="products-row-actions">
                    <button type="button" onClick={() => openEditForm(p)} disabled={rowBusy[p.id]}>
                      Edit
                    </button>
                    <RoleGate roles={['Manager', 'Admin']}>
                      <button type="button" onClick={() => openPickerForProduct(p)} disabled={rowBusy[p.id]}>
                        {p.etimsItemClassificationCode ? 'Reclassify' : 'Classify'}
                      </button>
                    </RoleGate>
                    <RoleGate roles={['Manager', 'Admin']}>
                      {getEtimsStatus(p) === 'classified' && (
                        <button type="button" onClick={() => void handleRegister(p)} disabled={rowBusy[p.id]}>
                          {rowBusy[p.id] ? 'Registering…' : 'Register'}
                        </button>
                      )}
                    </RoleGate>
                    <Link to={`/stock/receive?productId=${p.id}`} className="products-receive-link">
                      Receive stock →
                    </Link>
                    {p.isActive && (
                      <button
                        type="button"
                        className="products-danger-btn"
                        disabled={rowBusy[p.id]}
                        onClick={() => void handleDeactivate(p)}
                      >
                        Deactivate
                      </button>
                    )}
                    {rowError[p.id] && <p className="products-error products-error--row">{rowError[p.id]}</p>}
                  </td>
                </tr>
              ))}
              {products.length === 0 && (
                <tr>
                  <td colSpan={9} className="products-hint">No products found.</td>
                </tr>
              )}
            </tbody>
          </table>

          {totalPages > 1 && (
            <div className="products-pagination">
              <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}>
                ← Prev
              </button>
              <span>Page {page} of {totalPages}</span>
              <button type="button" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}>
                Next →
              </button>
            </div>
          )}
        </>
      )}

      {pickerTargets && (
        <EtimsClassificationPicker
          products={pickerTargets}
          onClose={closePicker}
          onAssigned={handleAssigned}
        />
      )}
    </div>
  )
}