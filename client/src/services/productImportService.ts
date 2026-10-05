import { apiClient } from './apiClient'

export interface ProductImportRow {
  sku?: string | null
  barcode?: string | null
  name?: string | null
  categoryName?: string | null
  costPrice?: string | null
  salePrice?: string | null
  taxClass?: string | null
  reorderThreshold?: string | null
  warrantyMonths?: string | null
  openingQuantity?: string | null
  serialTracked?: string | null
}

export interface ProductImportRowResult {
  rowNumber: number
  row: ProductImportRow
  action: 'Create' | 'Skip' | 'Error'
  errors: string[]
  warnings: string[]
  resolvedSku: string | null
  willCreateCategory: boolean
}

export interface ProductImportPreview {
  rows: ProductImportRowResult[]
  totalRows: number
  willCreate: number
  willSkip: number
  errors: number
  newCategories: string[]
}

export interface ProductImportCommitResult {
  created: number
  categoriesCreated: number
  openingStockApplied: number
  skipped: number
  errors: number
}

export async function previewProductImport(file: File): Promise<ProductImportPreview> {
  const form = new FormData()
  form.append('file', file)
  const { data } = await apiClient.post<ProductImportPreview>('/api/products/import/preview', form)
  return data
}

export async function commitProductImport(rows: ProductImportRow[]): Promise<ProductImportCommitResult> {
  const { data } = await apiClient.post<ProductImportCommitResult>('/api/products/import/commit', rows)
  return data
}

/** Downloads the template through the authenticated client (a plain link would lack the token). */
export async function downloadImportTemplate(): Promise<void> {
  const { data } = await apiClient.get<Blob>('/api/products/import/template', { responseType: 'blob' })
  const url = URL.createObjectURL(data)
  const a = document.createElement('a')
  a.href = url
  a.download = 'products-import-template.csv'
  a.click()
  URL.revokeObjectURL(url)
}