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
import type { Category, Product, ProductFormValues, TaxClass } from '../types/product'
import type { ApiErrorBody } from '../types/auth'
import { formatKes } from '../utils/currency'
import './ProductsPage.css'

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

  const [formOpen, setFormOpen] = useState(false)
  const [editingId, setEditingId] = useState<string | null>(null)
  const [formValues, setFormValues] = useState<ProductFormValues>(EMPTY_FORM)
  const [formImage, setFormImage] = useState<File | null>(null)
  const [formSubmitting, setFormSubmitting] = useState(false)
  const [formError, setFormError] = useState<string | null>(null)

  const [rowBusy, setRowBusy] = useState<Record<string, boolean>>({})
  const [rowError, setRowError] = useState<Record<string, string>>({})

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
      const res = await listProducts({ page, pageSize: PAGE_SIZE, search: search || undefined })
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
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, search])

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

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE))

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
      </div>

      {loading && <p className="products-hint">Loading products…</p>}
      {loadError && <p className="products-error" role="alert">{loadError}</p>}

      {!loading && !loadError && (
        <>
          <table className="products-table">
            <thead>
              <tr>
                <th>SKU</th>
                <th>Name</th>
                <th>Category</th>
                <th>Price</th>
                <th>Tax</th>
                <th>Stock</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {products.map((p) => (
                <tr key={p.id} className={p.isActive ? '' : 'products-row--inactive'}>
                  <td>{p.sku}</td>
                  <td>{p.name}</td>
                  <td>{p.categoryName}</td>
                  <td className="products-col-price">{formatKes(p.salePrice)}</td>
                  <td className="products-col-tax">{p.taxClass}</td>
                  <td className={`products-col-stock ${p.stockCount <= p.reorderThreshold ? 'products-stock--low' : ''}`}>
                    {p.stockCount}
                  </td>
                  <td className="products-row-actions">
                    <button type="button" onClick={() => openEditForm(p)} disabled={rowBusy[p.id]}>
                      Edit
                    </button>
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
                  <td colSpan={7} className="products-hint">No products found.</td>
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
    </div>
  )
}