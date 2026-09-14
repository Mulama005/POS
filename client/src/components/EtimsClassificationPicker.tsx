import { useEffect, useRef, useState, type KeyboardEvent } from 'react'
import { isAxiosError } from 'axios'
import {
  assignEtimsClassification,
  getEtimsItemClassChildren,
  searchEtimsItemClasses,
} from '../services/EtimsService'
import type { EtimsItemClassOption } from '../types/etims'
import type { ApiErrorBody } from '../types/auth'
import './EtimsClassificationPicker.css'

export interface EtimsPickerTarget {
  id: string
  sku: string
  name: string
  /** Current classification code, if any. */
  currentCode: string | null
}

interface EtimsClassificationPickerProps {
  products: EtimsPickerTarget[]
  onClose: () => void
  /** Called after successful assignment so the caller can refetch and close. */
  onAssigned: () => void
}

function getErrorMessage(err: unknown, fallback: string): string {
  if (isAxiosError<ApiErrorBody>(err) && err.response?.data?.message) {
    return err.response.data.message
  }

  return fallback
}

const SEARCH_DEBOUNCE_MS = 300
const MIN_QUERY_LENGTH = 3

type PickerMode = 'search' | 'browse'

interface BrowseLevel {
  code: string | null
  name: string
  level: number
}

export function EtimsClassificationPicker({
  products,
  onClose,
  onAssigned,
}: EtimsClassificationPickerProps) {
  const [mode, setMode] = useState<PickerMode>('search')

  const [query, setQuery] = useState('')
  const [results, setResults] = useState<EtimsItemClassOption[]>([])
  const [total, setTotal] = useState(0)
  const [searching, setSearching] = useState(false)
  const [searchError, setSearchError] = useState<string | null>(null)
  const [highlightedIndex, setHighlightedIndex] = useState(-1)

  const [browseItems, setBrowseItems] = useState<EtimsItemClassOption[]>([])
  const [browseLoading, setBrowseLoading] = useState(false)
  const [browseError, setBrowseError] = useState<string | null>(null)
  const [browsePath, setBrowsePath] = useState<BrowseLevel[]>([])
  const [browseParent, setBrowseParent] = useState<EtimsItemClassOption | null>(null)

  const [selected, setSelected] = useState<EtimsItemClassOption | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [submitError, setSubmitError] = useState<string | null>(null)

  const searchInputRef = useRef<HTMLInputElement>(null)
  const resultListRef = useRef<HTMLUListElement>(null)

  const trimmedQuery = query.trim()
  const queryTooShort = trimmedQuery.length < MIN_QUERY_LENGTH

  useEffect(() => {
    searchInputRef.current?.focus()
  }, [])

  /*
   * Search
   */
  useEffect(() => {
    if (mode !== 'search' || queryTooShort) return

    const handle = setTimeout(() => {
      setSearching(true)
      setSearchError(null)

      searchEtimsItemClasses(trimmedQuery)
        .then((res) => {
          setResults(res.items)
          setTotal(res.total)
          setHighlightedIndex(res.items.length > 0 ? 0 : -1)
        })
        .catch((err: unknown) => {
          setSearchError(
            getErrorMessage(
              err,
              'Could not search KRA classifications.',
            ),
          )
          setResults([])
          setTotal(0)
        })
        .finally(() => setSearching(false))
    }, SEARCH_DEBOUNCE_MS)

    return () => clearTimeout(handle)
  }, [mode, trimmedQuery, queryTooShort])

  /*
   * Browse
   */
  const loadBrowseLevel = async (
    parentCode?: string,
    nextPath?: BrowseLevel[],
  ) => {
    setBrowseLoading(true)
    setBrowseError(null)

    try {
      const response = await getEtimsItemClassChildren(parentCode)

      setBrowseItems(response.items)
      setBrowseParent(response.parent)

      if (nextPath) {
        setBrowsePath(nextPath)
      }
    } catch (err) {
      setBrowseError(
        getErrorMessage(
          err,
          'Could not load this part of the KRA taxonomy.',
        ),
      )
      setBrowseItems([])
    } finally {
      setBrowseLoading(false)
    }
  }

  const handleBrowseMode = () => {
    setMode('browse')
    setQuery('')
    setResults([])
    setTotal(0)
    setSearchError(null)
    setHighlightedIndex(-1)

    if (browsePath.length === 0) {
      void loadBrowseLevel(undefined, [])
    }
  }

  const handleSearchMode = () => {
    setMode('search')
    setSearchError(null)

    window.setTimeout(() => {
      searchInputRef.current?.focus()
    }, 0)
  }

  const handleBrowseItem = (item: EtimsItemClassOption) => {
    if (item.selectable) {
      setSelected(item)
      return
    }

    if (!item.hasChildren) return

    const nextPath: BrowseLevel[] = [
      ...browsePath,
      {
        code: item.itemClsCd,
        name: item.itemClsNm,
        level: item.itemClsLvl,
      },
    ]

    void loadBrowseLevel(item.itemClsCd, nextPath)
  }

  const handleBrowseBack = () => {
    if (browsePath.length === 0) return

    const nextPath = browsePath.slice(0, -1)
    const parentCode = nextPath.length > 0
      ? nextPath[nextPath.length - 1].code ?? undefined
      : undefined

    void loadBrowseLevel(parentCode, nextPath)
  }

  const handleBrowseBreadcrumb = (index: number) => {
    if (index === -1) {
      void loadBrowseLevel(undefined, [])
      return
    }

    const targetPath = browsePath.slice(0, index + 1)
    const parentCode = targetPath[targetPath.length - 1]?.code ?? undefined

    void loadBrowseLevel(parentCode, targetPath)
  }

  const handleSearchKeyDown = (e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Escape') {
      onClose()
      return
    }

    if (queryTooShort || results.length === 0) return

    if (e.key === 'ArrowDown') {
      e.preventDefault()
      setHighlightedIndex((i) => (i + 1) % results.length)
    } else if (e.key === 'ArrowUp') {
      e.preventDefault()
      setHighlightedIndex(
        (i) => (i - 1 + results.length) % results.length,
      )
    } else if (e.key === 'Enter' && highlightedIndex >= 0) {
      e.preventDefault()
      setSelected(results[highlightedIndex])
    }
  }

  useEffect(() => {
    if (
      mode !== 'search' ||
      highlightedIndex < 0 ||
      !resultListRef.current
    ) {
      return
    }

    const el = resultListRef.current.children[
      highlightedIndex
    ] as HTMLElement | undefined

    el?.scrollIntoView({ block: 'nearest' })
  }, [mode, highlightedIndex])

  const handleConfirm = async () => {
    if (!selected) return

    setSubmitting(true)
    setSubmitError(null)

    try {
      await assignEtimsClassification(
        products.map((p) => p.id),
        selected.itemClsCd,
      )

      onAssigned()
    } catch (err) {
      setSubmitError(
        getErrorMessage(
          err,
          'Could not assign this classification. Nothing was changed.',
        ),
      )
    } finally {
      setSubmitting(false)
    }
  }

  const reclassifyTargets = products.filter(
    (p) =>
      p.currentCode &&
      p.currentCode !== selected?.itemClsCd,
  )

  const isBatch = products.length > 1

  return (
    <div
      className="etims-picker-backdrop"
      role="dialog"
      aria-modal="true"
      aria-label="Assign eTIMS classification"
    >
      <div className="etims-picker">
        <div className="etims-picker__header">
          <h2>Assign eTIMS classification</h2>

          <button
            type="button"
            className="etims-picker__close"
            onClick={onClose}
            aria-label="Close"
          >
            ✕
          </button>
        </div>

        <p className="etims-picker__target-summary">
          {isBatch ? (
            <>
              Applying to <strong>{products.length} products</strong>:{' '}
              {products.map((p) => p.sku).join(', ')}
            </>
          ) : (
            <>
              Classifying <strong>{products[0]?.sku}</strong> —{' '}
              {products[0]?.name}
            </>
          )}
        </p>

        {!selected ? (
          <>
            <div className="etims-picker__tabs" role="tablist">
              <button
                type="button"
                role="tab"
                aria-selected={mode === 'search'}
                className={
                  mode === 'search'
                    ? 'etims-picker__tab etims-picker__tab--active'
                    : 'etims-picker__tab'
                }
                onClick={handleSearchMode}
              >
                Search
              </button>

              <button
                type="button"
                role="tab"
                aria-selected={mode === 'browse'}
                className={
                  mode === 'browse'
                    ? 'etims-picker__tab etims-picker__tab--active'
                    : 'etims-picker__tab'
                }
                onClick={handleBrowseMode}
              >
                Browse
              </button>
            </div>

            {mode === 'search' ? (
              <>
                <label
                  className="etims-picker__search-label"
                  htmlFor="etims-picker-search"
                >
                  Search KRA's product taxonomy
                </label>

                <input
                  id="etims-picker-search"
                  ref={searchInputRef}
                  type="text"
                  className="etims-picker__search-input"
                  placeholder="e.g. mobile phone, charger, battery…"
                  value={query}
                  onChange={(e) => setQuery(e.target.value)}
                  onKeyDown={handleSearchKeyDown}
                  autoComplete="off"
                />

                <div className="etims-picker__results">
                  {trimmedQuery.length > 0 && queryTooShort && (
                    <p className="etims-picker__hint">
                      Keep typing — at least {MIN_QUERY_LENGTH} characters.
                    </p>
                  )}

                  {searching && (
                    <p className="etims-picker__hint">
                      Searching…
                    </p>
                  )}

                  {searchError && (
                    <p
                      className="etims-picker__error"
                      role="alert"
                    >
                      {searchError}
                    </p>
                  )}

                  {!searching &&
                    !searchError &&
                    !queryTooShort &&
                    results.length === 0 && (
                      <p className="etims-picker__hint">
                        No matches for &ldquo;{trimmedQuery}&rdquo;.
                      </p>
                    )}

                  {results.length > 0 && (
                    <>
                      <ul
                        className="etims-picker__result-list"
                        ref={resultListRef}
                        role="listbox"
                      >
                        {results.map((r, i) => (
                          <li key={r.itemClsCd}>
                            <button
                              type="button"
                              role="option"
                              aria-selected={
                                i === highlightedIndex
                              }
                              className={
                                i === highlightedIndex
                                  ? 'etims-picker__result etims-picker__result--highlighted'
                                  : 'etims-picker__result'
                              }
                              onMouseEnter={() =>
                                setHighlightedIndex(i)
                              }
                              onClick={() => setSelected(r)}
                            >
                              <span className="etims-picker__result-name">
                                {r.itemClsNm}
                              </span>

                              <span className="etims-picker__result-meta">
                                <code>{r.itemClsCd}</code>

                                {r.taxTyCd && (
                                  <span className="etims-picker__result-tax">
                                    Tax {r.taxTyCd}
                                  </span>
                                )}
                              </span>
                            </button>
                          </li>
                        ))}
                      </ul>

                      {total > results.length && (
                        <p className="etims-picker__hint etims-picker__hint--more">
                          {total - results.length} more match
                          {total - results.length === 1
                            ? ''
                            : 'es'}{' '}
                          — refine your search to narrow it down.
                        </p>
                      )}
                    </>
                  )}
                </div>
              </>
            ) : (
              <div className="etims-picker__browse">
                <div className="etims-picker__breadcrumbs">
                  <button
                    type="button"
                    className="etims-picker__breadcrumb"
                    onClick={() =>
                      handleBrowseBreadcrumb(-1)
                    }
                  >
                    All categories
                  </button>

                  {browsePath.map((level, index) => (
                    <span
                      className="etims-picker__breadcrumb-wrap"
                      key={level.code}
                    >
                      <span className="etims-picker__breadcrumb-separator">
                        /
                      </span>

                      <button
                        type="button"
                        className="etims-picker__breadcrumb"
                        onClick={() =>
                          handleBrowseBreadcrumb(index)
                        }
                      >
                        {level.name}
                      </button>
                    </span>
                  ))}
                </div>

                {browsePath.length > 0 && (
                  <button
                    type="button"
                    className="etims-picker__browse-back"
                    onClick={handleBrowseBack}
                    disabled={browseLoading}
                  >
                    ← Back
                  </button>
                )}

                {browseLoading && (
                  <p className="etims-picker__hint">
                    Loading classifications…
                  </p>
                )}

                {browseError && (
                  <p
                    className="etims-picker__error"
                    role="alert"
                  >
                    {browseError}
                  </p>
                )}

                {!browseLoading &&
                  !browseError &&
                  browseItems.length === 0 && (
                    <p className="etims-picker__hint">
                      No classifications found at this level.
                    </p>
                  )}

                {!browseLoading &&
                  !browseError &&
                  browseItems.length > 0 && (
                    <ul className="etims-picker__browse-list">
                      {browseItems.map((item) => (
                        <li key={item.itemClsCd}>
                          <button
                            type="button"
                            className="etims-picker__browse-item"
                            onClick={() =>
                              handleBrowseItem(item)
                            }
                          >
                            <span className="etims-picker__browse-item-main">
                              <span className="etims-picker__browse-item-name">
                                {item.itemClsNm}
                              </span>

                              <span className="etims-picker__browse-item-meta">
                                <code>{item.itemClsCd}</code>
                                <span>
                                  Level {item.itemClsLvl}
                                </span>
                              </span>
                            </span>

                            {item.selectable ? (
                              <span className="etims-picker__browse-select">
                                Select
                              </span>
                            ) : (
                              <span className="etims-picker__browse-chevron">
                                →
                              </span>
                            )}
                          </button>
                        </li>
                      ))}
                    </ul>
                  )}
              </div>
            )}

            <div className="etims-picker__actions">
              <button
                type="button"
                onClick={onClose}
              >
                Cancel
              </button>
            </div>
          </>
        ) : (
          <div className="etims-picker__confirm">
            <div className="etims-picker__confirm-chosen">
              <span className="etims-picker__confirm-label">
                Selected classification
              </span>

              <span className="etims-picker__confirm-name">
                {selected.itemClsNm}
              </span>

              <span className="etims-picker__confirm-meta">
                <code>{selected.itemClsCd}</code>

                {selected.taxTyCd && (
                  <span className="etims-picker__result-tax">
                    Tax type {selected.taxTyCd}
                  </span>
                )}
              </span>
            </div>

            <ul className="etims-picker__confirm-products">
              {products.map((p) => (
                <li key={p.id}>
                  <span>
                    {p.sku} — {p.name}
                  </span>

                  {p.currentCode &&
                    p.currentCode !== selected.itemClsCd && (
                      <span className="etims-picker__reclassify-flag">
                        currently {p.currentCode} — will be replaced
                      </span>
                    )}
                </li>
              ))}
            </ul>

            {reclassifyTargets.length > 0 && (
              <p
                className="etims-picker__warning"
                role="alert"
              >
                {reclassifyTargets.length} of these{' '}
                {reclassifyTargets.length === 1
                  ? 'is'
                  : 'are'}{' '}
                already classified differently. Confirming will
                overwrite{' '}
                {reclassifyTargets.length === 1
                  ? 'it'
                  : 'them'}.
              </p>
            )}

            {submitError && (
              <p
                className="etims-picker__error"
                role="alert"
              >
                {submitError}
              </p>
            )}

            <div className="etims-picker__actions">
              <button
                type="button"
                disabled={submitting}
                onClick={() => setSelected(null)}
              >
                ← Back
              </button>

              <button
                type="button"
                className="etims-picker__confirm-btn"
                disabled={submitting}
                onClick={() => void handleConfirm()}
              >
                {submitting
                  ? 'Assigning…'
                  : `Assign to ${products.length} product${
                      products.length === 1 ? '' : 's'
                    }`}
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}