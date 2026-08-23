import { useEffect, useRef, useState } from 'react'
import { lookupProductByBarcode, searchProducts } from '../services/ProductsService'
import type { ProductSummary } from '../types/product'

interface ProductSearchBarProps {
  onAdd: (product: ProductSummary) => void
  disabled?: boolean
}

// A physical barcode scanner types the full code and an Enter keystroke faster than a
// human could — heuristically, anything that's mostly digits and reasonably long is
// almost certainly a scan, not someone typing a product name.
function looksLikeBarcode(value: string): boolean {
  return /^\d{6,}$/.test(value.trim())
}

export function ProductSearchBar({ onAdd, disabled }: ProductSearchBarProps) {
  const [query, setQuery] = useState('')
  const [results, setResults] = useState<ProductSummary[]>([])
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    const trimmed = query.trim()
    if (trimmed.length < 2 || looksLikeBarcode(trimmed)) {
      setResults([])
      return
    }

    const timeout = setTimeout(() => {
      setLoading(true)
      searchProducts(trimmed)
        .then(setResults)
        .catch(() => setResults([]))
        .finally(() => setLoading(false))
    }, 250)

    return () => clearTimeout(timeout)
  }, [query])

  const handleAdd = (product: ProductSummary) => {
    onAdd(product)
    setQuery('')
    setResults([])
    inputRef.current?.focus()
  }

  const handleKeyDown = async (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (e.key !== 'Enter') return
    const trimmed = query.trim()
    if (!trimmed) return

    e.preventDefault()
    setError(null)

    if (looksLikeBarcode(trimmed)) {
      setLoading(true)
      try {
        const product = await lookupProductByBarcode(trimmed)
        if (product) {
          handleAdd(product)
        } else {
          setError(`No product found for barcode ${trimmed}.`)
        }
      } finally {
        setLoading(false)
      }
      return
    }

    // Typed search + Enter: if it narrowed to exactly one match, add it directly —
    // otherwise leave the dropdown open for the cashier to pick.
    if (results.length === 1) {
      handleAdd(results[0])
    }
  }

  return (
    <div className="product-search">
      <input
        ref={inputRef}
        type="text"
        className="product-search__input"
        placeholder="Scan barcode or search by name / SKU…"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        onKeyDown={handleKeyDown}
        disabled={disabled}
        autoFocus
      />
      {loading && <div className="product-search__hint">Searching…</div>}
      {error && <div className="product-search__error">{error}</div>}
      {results.length > 0 && (
        <ul className="product-search__results">
          {results.map((product) => (
            <li key={product.id}>
              <button type="button" onClick={() => handleAdd(product)}>
                <span className="product-search__result-name">{product.name}</span>
                <span className="product-search__result-meta">
                  {product.sku} · KES {product.salePrice.toFixed(2)} · {product.stockQuantity} in stock
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}