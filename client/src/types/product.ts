export type TaxClass = 'Standard' | 'ZeroRated' | 'Exempt'

/** Matches ProductSummaryDto from ProductsController — the read-only lookup
 * contract Step 18's full product module extends rather than replaces. */
export interface ProductSummary {
  id: string
  sku: string
  barcode: string
  name: string
  categoryId: string
  categoryName: string
  salePrice: number
  taxClass: TaxClass
  stockQuantity: number
  imageUrl: string | null
}