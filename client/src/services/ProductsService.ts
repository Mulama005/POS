import { apiClient } from './apiClient'
import type { ProductSummary } from '../types/product'

export async function searchProducts(query: string): Promise<ProductSummary[]> {
  if (!query.trim()) {
    return []
  }
  const { data } = await apiClient.get<ProductSummary[]>('/api/products/search', {
    params: { q: query },
  })
  return data
}

/** Returns null (rather than throwing) on a 404 — "no product with this barcode" is an
 * expected outcome of a scan, not an error state the caller needs to catch specially. */
export async function lookupProductByBarcode(barcode: string): Promise<ProductSummary | null> {
  try {
    const { data } = await apiClient.get<ProductSummary>('/api/products/lookup', {
      params: { barcode },
    })
    return data
  } catch (error: unknown) {
    if (isNotFound(error)) {
      return null
    }
    throw error
  }
}

function isNotFound(error: unknown): boolean {
  return (
    typeof error === 'object' &&
    error !== null &&
    'response' in error &&
    (error as { response?: { status?: number } }).response?.status === 404
  )
}