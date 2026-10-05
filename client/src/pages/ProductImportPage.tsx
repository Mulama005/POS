import { useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { isAxiosError } from 'axios'
import {
  commitProductImport,
  downloadImportTemplate,
  previewProductImport,
  type ProductImportCommitResult,
  type ProductImportPreview,
} from '../services/productImportService'
import './ProductImportPage.css'

function errorText(err: unknown): string {
  if (isAxiosError(err)) {
    const data = err.response?.data as { message?: string } | string | undefined
    if (typeof data === 'string' && data) return data
    if (data && typeof data === 'object' && data.message) return data.message
  }
  return 'Something went wrong. Please try again.'
}

export function ProductImportPage() {
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<ProductImportPreview | null>(null)
  const [result, setResult] = useState<ProductImportCommitResult | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [problemsOnly, setProblemsOnly] = useState(false)

  const visibleRows = useMemo(() => {
    if (!preview) return []
    return problemsOnly
      ? preview.rows.filter((r) => r.action !== 'Create' || r.warnings.length > 0)
      : preview.rows
  }, [preview, problemsOnly])

  const runPreview = async (selected: File) => {
    setFile(selected)
    setPreview(null)
    setResult(null)
    setError('')
    setBusy(true)
    try {
      setPreview(await previewProductImport(selected))
    } catch (e) {
      setError(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  const runCommit = async () => {
    if (!preview) return
    setBusy(true)
    setError('')
    try {
      setResult(await commitProductImport(preview.rows.map((r) => r.row)))
      setPreview(null)
      setFile(null)
    } catch (e) {
      setError(errorText(e))
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="products-screen import-screen">
      <div className="products-header">
        <h1 className="products-title">Import products</h1>
        <Link to="/inventory" className="import-back">← Back to products</Link>
      </div>

      {result ? (
        <section className="import-done">
          <h2>Import complete</h2>
          <div className="pos-ledger-row"><span>Products created</span><span className="pos-leader" /><span>{result.created}</span></div>
          <div className="pos-ledger-row"><span>New categories</span><span className="pos-leader" /><span>{result.categoriesCreated}</span></div>
          <div className="pos-ledger-row"><span>With opening stock</span><span className="pos-leader" /><span>{result.openingStockApplied}</span></div>
          <div className="pos-ledger-row"><span>Skipped (already existed)</span><span className="pos-leader" /><span>{result.skipped}</span></div>
          <p className="import-hint">
            Serial-tracked items (phones, laptops) start with no stock. Add their units, with serial numbers, under{' '}
            <Link to="/stock/receive">Receive stock</Link>.
          </p>
          <div className="import-actions">
            <Link to="/inventory" className="products-add-btn">View products</Link>
            <button type="button" className="import-secondary" onClick={() => setResult(null)}>Import another file</button>
          </div>
        </section>
      ) : (
        <>
          <section className="import-step">
            <p className="import-hint">
              Upload a CSV with one product per row. Leave <strong>Sku</strong> blank to have one generated from the
              category (e.g. <code>PHO-0001</code>). Nothing is saved until you review the preview and confirm.
            </p>
            <div className="import-actions">
              <label className="products-add-btn import-file">
                {file ? 'Choose a different file' : 'Choose CSV file'}
                <input
                  type="file"
                  accept=".csv,text/csv"
                  disabled={busy}
                  onChange={(e) => {
                    const f = e.target.files?.[0]
                    e.target.value = ''
                    if (f) void runPreview(f)
                  }}
                />
              </label>
              <button type="button" className="import-secondary" onClick={() => void downloadImportTemplate()}>
                Download template
              </button>
              {file && <span className="import-filename">{file.name}</span>}
            </div>
          </section>

          {busy && <p className="import-hint">Working…</p>}
          {error && <p className="import-error" role="alert">{error}</p>}

          {preview && (
            <section className="import-step">
              <div className="import-summary">
                <div className="pos-ledger-row"><span>Rows in file</span><span className="pos-leader" /><span>{preview.totalRows}</span></div>
                <div className="pos-ledger-row"><span>Will be created</span><span className="pos-leader" /><span>{preview.willCreate}</span></div>
                <div className="pos-ledger-row"><span>Already exist (skipped)</span><span className="pos-leader" /><span>{preview.willSkip}</span></div>
                <div className="pos-ledger-row"><span>Rows with errors</span><span className="pos-leader" /><span className={preview.errors ? 'import-bad' : ''}>{preview.errors}</span></div>
                {preview.newCategories.length > 0 && (
                  <div className="pos-ledger-row"><span>New categories</span><span className="pos-leader" /><span>{preview.newCategories.join(', ')}</span></div>
                )}
              </div>

              <label className="import-toggle">
                <input type="checkbox" checked={problemsOnly} onChange={(e) => setProblemsOnly(e.target.checked)} />
                Show only rows needing attention
              </label>

              <div className="import-table-wrap">
                <table className="import-table">
                  <thead>
                    <tr><th>Row</th><th>Result</th><th>SKU</th><th>Name</th><th>Category</th><th>Cost</th><th>Price</th><th>Qty</th><th>Notes</th></tr>
                  </thead>
                  <tbody>
                    {visibleRows.map((r) => (
                      <tr key={r.rowNumber} className={`import-row--${r.action.toLowerCase()}`}>
                        <td>{r.rowNumber}</td>
                        <td>
                          <span className={`pos-badge pos-badge--${r.action === 'Create' ? 'success' : r.action === 'Skip' ? 'neutral' : 'danger'}`}>
                            {r.action}
                          </span>
                        </td>
                        <td>{r.resolvedSku}</td>
                        <td>{r.row.name}</td>
                        <td>{r.row.categoryName}</td>
                        <td>{r.row.costPrice}</td>
                        <td>{r.row.salePrice}</td>
                        <td>{r.row.openingQuantity}</td>
                        <td className="import-notes">
                          {r.errors.map((m) => <div key={m} className="import-bad">{m}</div>)}
                          {r.warnings.map((m) => <div key={m}>{m}</div>)}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>

              <div className="import-actions">
                <button
                  type="button"
                  className="products-add-btn"
                  disabled={busy || preview.willCreate === 0}
                  onClick={() => void runCommit()}
                >
                  Import {preview.willCreate} product{preview.willCreate === 1 ? '' : 's'}
                </button>
                {preview.errors > 0 && (
                  <span className="import-hint">
                    {preview.errors} row{preview.errors === 1 ? '' : 's'} with errors will be left out. Fix the file and upload it again to add them.
                  </span>
                )}
              </div>
            </section>
          )}
        </>
      )}
    </div>
  )
}