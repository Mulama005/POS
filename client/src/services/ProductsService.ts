import { apiClient } from './apiClient'
import type { Product, ProductFormValues, ProductListResponse, ProductSummary } from '../types/product'

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

// --- Management CRUD (ProductsPage) — distinct from the two read-only lookups above,
// which back checkout's search box. These hit ProductController's full CRUD surface. ---

export interface ListProductsParams {
  page?: number
  pageSize?: number
  search?: string
  category?: string
}

export async function listProducts(params: ListProductsParams = {}): Promise<ProductListResponse> {
  const { data } = await apiClient.get<ProductListResponse>('/api/products', { params })
  return data
}

export async function getProduct(id: string): Promise<Product> {
  const { data } = await apiClient.get<Product>(`/api/products/${id}`)
  return data
}

/** ProductController's Create/Update endpoints bind [FromForm], not JSON, so
 * an optional image can ride along in the same request — build a FormData
 * with field names matching CreateProductRequest's C# properties. */
function toFormData(values: ProductFormValues, image: File | null): FormData {
  const form = new FormData()
  form.append('Sku', values.sku)
  form.append('Barcode', values.barcode)
  form.append('Name', values.name)
  form.append('Description', values.description)
  form.append('CategoryId', values.categoryId)
  form.append('CostPrice', values.costPrice)
  form.append('SalePrice', values.salePrice)
  form.append('TaxClass', values.taxClass)
  form.append('ReorderThreshold', values.reorderThreshold)
  form.append('WarrantyMonths', values.warrantyMonths)
  if (image) {
    form.append('image', image)
  }
  return form
}

export async function createProduct(values: ProductFormValues, image: File | null): Promise<Product> {
  const { data } = await apiClient.post<Product>('/api/products', toFormData(values, image), {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data
}

export async function updateProduct(id: string, values: ProductFormValues, image: File | null): Promise<Product> {
  const { data } = await apiClient.put<Product>(`/api/products/${id}`, toFormData(values, image), {
    headers: { 'Content-Type': 'multipart/form-data' },
  })
  return data
}

/** Soft delete — ProductController.Delete sets IsActive = false rather than
 * removing the row (sale history keeps pointing at a real product). */
export async function deleteProduct(id: string): Promise<void> {
  await apiClient.delete(`/api/products/${id}`)
}